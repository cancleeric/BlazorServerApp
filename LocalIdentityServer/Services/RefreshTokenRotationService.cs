using System.Security.Cryptography;
using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace LocalIdentityServer.Services;

/// <summary>
/// Refresh Token 輪替服務實作 - 遵循 SOLID 原則
/// 實作 Token 輪替、重用偵測與安全回應機制
/// </summary>
public class RefreshTokenRotationService : IRefreshTokenRotationService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<RefreshTokenRotationService> _logger;

    private const int TOKEN_VALIDITY_DAYS = 30;
    private const string REUSE_DETECTION_REASON = "Token reuse detected - possible replay attack";
    private const string EMERGENCY_REVOKE_REASON = "Emergency revocation due to security threat";

    public RefreshTokenRotationService(
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<RefreshTokenRotationService> logger)
    {
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenRotationResult> RotateTokenAsync(string oldToken, string clientId, string[] scopes)
    {
        try
        {
            _logger.LogDebug("Starting token rotation for client: {ClientId}", clientId);

            // 檢查 Token 重用
            var securityCheck = await CheckTokenReuseAsync(oldToken);
            if (!securityCheck.IsSecure)
            {
                _logger.LogWarning("Security threat detected during token rotation. Threat level: {ThreatLevel}", 
                    securityCheck.ThreatLevel);
                return new TokenRotationResult
                {
                    Success = false,
                    ErrorMessage = "Security threat detected",
                    SecurityCheck = securityCheck
                };
            }

            // 取得舊 Token
            var oldTokenEntity = await _refreshTokenRepository.GetByTokenAsync(oldToken);
            if (oldTokenEntity == null)
            {
                return new TokenRotationResult
                {
                    Success = false,
                    ErrorMessage = "Token not found"
                };
            }

            // 驗證 Token 有效性
            if (oldTokenEntity.IsUsed || oldTokenEntity.IsRevoked || oldTokenEntity.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Attempt to rotate invalid token. Used: {IsUsed}, Revoked: {IsRevoked}", 
                    oldTokenEntity.IsUsed, oldTokenEntity.IsRevoked);
                
                // 可能的重用攻擊，撤銷整個家族
                await EmergencyRevokeTokenFamilyAsync(oldTokenEntity.TokenFamily, REUSE_DETECTION_REASON);
                
                return new TokenRotationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid token - family revoked for security",
                    SecurityCheck = new SecurityCheckResult
                    {
                        IsSecure = false,
                        IsTokenReused = true,
                        ThreatLevel = SecurityThreatLevel.High,
                        ThreatDescription = "Attempt to use invalid token"
                    }
                };
            }

            // 產生新 Token
            var newToken = GenerateSecureToken();
            var newTokenEntity = new RefreshTokenEntity
            {
                Token = newToken,
                ClientId = oldTokenEntity.ClientId,
                Subject = oldTokenEntity.Subject,
                Username = oldTokenEntity.Username,
                Email = oldTokenEntity.Email,
                Scope = string.Join(" ", scopes),
                TokenFamily = oldTokenEntity.TokenFamily,
                ExpiresAt = DateTime.UtcNow.AddDays(TOKEN_VALIDITY_DAYS),
                CreatedAt = DateTime.UtcNow,
                IsUsed = false,
                IsRevoked = false
            };

            // 儲存新 Token
            await _refreshTokenRepository.StoreAsync(newTokenEntity);

            // 標記舊 Token 為已使用
            await _refreshTokenRepository.UseTokenAsync(oldToken, newToken);

            _logger.LogInformation("Token rotation completed successfully for family: {TokenFamily}", 
                oldTokenEntity.TokenFamily);

            return new TokenRotationResult
            {
                Success = true,
                NewToken = newToken,
                SecurityCheck = securityCheck
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token rotation");
            return new TokenRotationResult
            {
                Success = false,
                ErrorMessage = "Internal error during token rotation"
            };
        }
    }

    public async Task<TokenCreationResult> CreateTokenFamilyAsync(UserEntity user, ClientEntity client, string[] scopes)
    {
        try
        {
            _logger.LogDebug("Creating new token family for user: {Username}, client: {ClientId}", 
                user.UserName, client.ClientId);

            var tokenFamily = Guid.NewGuid().ToString("N");
            var token = GenerateSecureToken();

            var tokenEntity = new RefreshTokenEntity
            {
                Token = token,
                ClientId = client.ClientId,
                Subject = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Scope = string.Join(" ", scopes),
                TokenFamily = tokenFamily,
                ExpiresAt = DateTime.UtcNow.AddDays(TOKEN_VALIDITY_DAYS),
                CreatedAt = DateTime.UtcNow,
                IsUsed = false,
                IsRevoked = false
            };

            await _refreshTokenRepository.StoreAsync(tokenEntity);

            _logger.LogInformation("New token family created: {TokenFamily}", tokenFamily);

            return new TokenCreationResult
            {
                Success = true,
                Token = token,
                TokenFamily = tokenFamily,
                ExpiresAt = tokenEntity.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating token family");
            return new TokenCreationResult
            {
                Success = false,
                ErrorMessage = "Failed to create token family"
            };
        }
    }

    public async Task<SecurityCheckResult> CheckTokenReuseAsync(string token)
    {
        try
        {
            _logger.LogDebug("Performing security check for token reuse");

            var isReused = await _refreshTokenRepository.IsTokenReusedAsync(token);
            
            if (isReused)
            {
                var tokenEntity = await _refreshTokenRepository.GetByTokenAsync(token);
                if (tokenEntity != null)
                {
                    _logger.LogWarning("Token reuse detected for family: {TokenFamily}", tokenEntity.TokenFamily);
                    
                    // 觸發緊急撤銷
                    await EmergencyRevokeTokenFamilyAsync(tokenEntity.TokenFamily, REUSE_DETECTION_REASON);
                    
                    return new SecurityCheckResult
                    {
                        IsSecure = false,
                        IsTokenReused = true,
                        ThreatLevel = SecurityThreatLevel.Critical,
                        ThreatDescription = "Token reuse detected - possible replay attack",
                        RecommendedActions = new List<string>
                        {
                            "Token family has been revoked",
                            "User should re-authenticate",
                            "Monitor for additional suspicious activity",
                            "Consider rate limiting for this client"
                        }
                    };
                }
            }

            return new SecurityCheckResult
            {
                IsSecure = true,
                IsTokenReused = false,
                ThreatLevel = SecurityThreatLevel.None,
                ThreatDescription = "No security threats detected"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during security check");
            return new SecurityCheckResult
            {
                IsSecure = false,
                ThreatLevel = SecurityThreatLevel.Medium,
                ThreatDescription = "Security check failed due to internal error"
            };
        }
    }

    public async Task EmergencyRevokeTokenFamilyAsync(string tokenFamily, string reason)
    {
        try
        {
            _logger.LogWarning("Emergency token family revocation initiated. Family: {TokenFamily}, Reason: {Reason}", 
                tokenFamily, reason);

            await _refreshTokenRepository.RevokeTokenFamilyAsync(tokenFamily, reason);

            _logger.LogInformation("Emergency token family revocation completed: {TokenFamily}", tokenFamily);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency token family revocation: {TokenFamily}", tokenFamily);
            throw;
        }
    }

    public async Task<TokenFamilyStatistics> GetTokenFamilyStatisticsAsync(string tokenFamily)
    {
        try
        {
            _logger.LogDebug("Retrieving statistics for token family: {TokenFamily}", tokenFamily);

            var familyTokens = await _refreshTokenRepository.GetByTokenFamilyAsync(tokenFamily);

            var statistics = new TokenFamilyStatistics
            {
                TokenFamily = tokenFamily,
                TotalTokens = familyTokens.Count,
                ActiveTokens = familyTokens.Count(t => !t.IsUsed && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow),
                UsedTokens = familyTokens.Count(t => t.IsUsed),
                RevokedTokens = familyTokens.Count(t => t.IsRevoked),
                CreatedAt = familyTokens.FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow,
                LastUsedAt = familyTokens.Where(t => t.UsedAt.HasValue).Max(t => t.UsedAt)
            };

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving token family statistics: {TokenFamily}", tokenFamily);
            throw;
        }
    }

    /// <summary>
    /// 產生安全的 Token
    /// </summary>
    private static string GenerateSecureToken()
    {
        var tokenBytes = new byte[32]; // 256 位元
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes);
    }
}