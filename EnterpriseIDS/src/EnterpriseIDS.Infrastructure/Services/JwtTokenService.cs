using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MSTokenValidationResult = Microsoft.IdentityModel.Tokens.TokenValidationResult;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Infrastructure.Services;

/// <summary>
/// JWT Token 服務實作
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IJwtTokenRepository _tokenRepository;
    private readonly ITokenBlacklistRepository _blacklistRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtTokenSettings _settings;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(
        IJwtTokenRepository tokenRepository,
        ITokenBlacklistRepository blacklistRepository,
        IUserRepository userRepository,
        ILogger<JwtTokenService> logger,
        IOptions<JwtTokenSettings> settings)
    {
        _tokenRepository = tokenRepository;
        _blacklistRepository = blacklistRepository;
        _userRepository = userRepository;
        _logger = logger;
        _settings = settings.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// 生成 Access Token
    /// </summary>
    public async Task<TokenResult> GenerateAccessTokenAsync(
        Guid userId,
        Guid tenantId,
        IEnumerable<Claim>? claims = null,
        IEnumerable<string>? scopes = null,
        string? clientId = null,
        string? audience = null,
        int? expirationMinutes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new ArgumentException($"使用者不存在: {userId}");
            }

            var jwtId = GenerateJwtId();
            var issuedAt = DateTime.UtcNow;
            var expiresAt = issuedAt.AddMinutes(expirationMinutes ?? _settings.AccessTokenExpirationMinutes);

            // 建立 Claims
            var tokenClaims = BuildAccessTokenClaims(user, tenantId, jwtId, issuedAt, clientId, scopes, claims);

            // 生成 JWT Token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(tokenClaims),
                Expires = expiresAt,
                Issuer = _settings.Issuer,
                Audience = audience ?? _settings.Audience,
                SigningCredentials = GetSigningCredentials(),
                NotBefore = issuedAt
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenValue = _tokenHandler.WriteToken(token);

            // 儲存到資料庫
            var jwtTokenEntity = new JwtToken
            {
                JwtId = jwtId,
                UserId = userId,
                TenantId = tenantId,
                TokenType = "access_token",
                TokenValue = ComputeTokenHash(tokenValue),
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt,
                Issuer = _settings.Issuer,
                Audience = audience ?? _settings.Audience,
                Subject = userId.ToString(),
                ClientId = clientId,
                Status = TokenStatus.Active
            };

            if (scopes != null)
            {
                jwtTokenEntity.SetScopes(scopes);
            }

            await _tokenRepository.AddAsync(jwtTokenEntity, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已生成 Access Token: {JwtId} for User: {UserId}", jwtId, userId);

            return new TokenResult
            {
                Token = tokenValue,
                JwtId = jwtId,
                TokenType = "access_token",
                ExpiresAt = expiresAt,
                IssuedAt = issuedAt,
                Scopes = scopes ?? Array.Empty<string>(),
                TokenId = jwtTokenEntity.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 Access Token 失敗: UserId={UserId}, TenantId={TenantId}", userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// 生成 Refresh Token
    /// </summary>
    public async Task<TokenResult> GenerateRefreshTokenAsync(
        Guid userId,
        Guid tenantId,
        string? clientId = null,
        IEnumerable<string>? scopes = null,
        int? expirationDays = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new ArgumentException($"使用者不存在: {userId}");
            }

            var jwtId = GenerateJwtId();
            var issuedAt = DateTime.UtcNow;
            var expiresAt = issuedAt.AddDays(expirationDays ?? _settings.RefreshTokenExpirationDays);

            // Refresh Token 使用簡單的隨機字串，不是 JWT
            var tokenValue = GenerateRefreshTokenValue();

            // 儲存到資料庫
            var jwtTokenEntity = new JwtToken
            {
                JwtId = jwtId,
                UserId = userId,
                TenantId = tenantId,
                TokenType = "refresh_token",
                TokenValue = ComputeTokenHash(tokenValue),
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt,
                Issuer = _settings.Issuer,
                Subject = userId.ToString(),
                ClientId = clientId,
                Status = TokenStatus.Active
            };

            if (scopes != null)
            {
                jwtTokenEntity.SetScopes(scopes);
            }

            await _tokenRepository.AddAsync(jwtTokenEntity, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已生成 Refresh Token: {JwtId} for User: {UserId}", jwtId, userId);

            return new TokenResult
            {
                Token = tokenValue,
                JwtId = jwtId,
                TokenType = "refresh_token",
                ExpiresAt = expiresAt,
                IssuedAt = issuedAt,
                Scopes = scopes ?? Array.Empty<string>(),
                TokenId = jwtTokenEntity.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 Refresh Token 失敗: UserId={UserId}, TenantId={TenantId}", userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// 生成 ID Token
    /// </summary>
    public async Task<TokenResult> GenerateIdTokenAsync(
        Guid userId,
        Guid tenantId,
        string audience,
        string? nonce = null,
        DateTime? authTime = null,
        IEnumerable<Claim>? additionalClaims = null,
        int? expirationMinutes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new ArgumentException($"使用者不存在: {userId}");
            }

            var jwtId = GenerateJwtId();
            var issuedAt = DateTime.UtcNow;
            var expiresAt = issuedAt.AddMinutes(expirationMinutes ?? _settings.IdTokenExpirationMinutes);

            // 建立 ID Token Claims
            var tokenClaims = BuildIdTokenClaims(user, tenantId, jwtId, issuedAt, authTime, nonce, additionalClaims);

            // 生成 JWT Token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(tokenClaims),
                Expires = expiresAt,
                Issuer = _settings.Issuer,
                Audience = audience,
                SigningCredentials = GetSigningCredentials(),
                NotBefore = issuedAt
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenValue = _tokenHandler.WriteToken(token);

            // 儲存到資料庫
            var jwtTokenEntity = new JwtToken
            {
                JwtId = jwtId,
                UserId = userId,
                TenantId = tenantId,
                TokenType = "id_token",
                TokenValue = ComputeTokenHash(tokenValue),
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt,
                Issuer = _settings.Issuer,
                Audience = audience,
                Subject = userId.ToString(),
                Status = TokenStatus.Active
            };

            await _tokenRepository.AddAsync(jwtTokenEntity, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已生成 ID Token: {JwtId} for User: {UserId}", jwtId, userId);

            return new TokenResult
            {
                Token = tokenValue,
                JwtId = jwtId,
                TokenType = "id_token",
                ExpiresAt = expiresAt,
                IssuedAt = issuedAt,
                Scopes = Array.Empty<string>(),
                TokenId = jwtTokenEntity.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 ID Token 失敗: UserId={UserId}, TenantId={TenantId}", userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// 驗證 JWT Token
    /// </summary>
    public async Task<Core.Interfaces.TokenValidationResult> ValidateTokenAsync(
        string token,
        bool validateLifetime = true,
        bool validateAudience = true,
        bool validateIssuer = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = validateIssuer,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = validateAudience,
                ValidAudience = _settings.Audience,
                ValidateLifetime = validateLifetime,
                IssuerSigningKey = GetSigningKey(),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(_settings.ClockSkewMinutes)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = validatedToken as JwtSecurityToken;

            if (jwtToken == null)
            {
                return new Core.Interfaces.TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid JWT token format",
                    ErrorCode = "invalid_token"
                };
            }

            var jwtId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jwtId))
            {
                return new Core.Interfaces.TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Missing JWT ID claim",
                    ErrorCode = "missing_jti"
                };
            }

            // 檢查黑名單
            var isBlacklisted = await IsTokenBlacklistedAsync(jwtId, cancellationToken);
            if (isBlacklisted)
            {
                return new Core.Interfaces.TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token is blacklisted",
                    ErrorCode = "token_blacklisted"
                };
            }

            // 檢查資料庫中的 Token
            var tokenEntity = await _tokenRepository.GetByJwtIdAsync(jwtId, cancellationToken);
            if (tokenEntity == null)
            {
                return new Core.Interfaces.TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token not found in database",
                    ErrorCode = "token_not_found"
                };
            }

            if (!tokenEntity.IsValid())
            {
                return new Core.Interfaces.TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token is not valid",
                    ErrorCode = "token_invalid"
                };
            }

            // 更新使用資訊
            await _tokenRepository.UpdateTokenUsageAsync(jwtId, cancellationToken: cancellationToken);

            return new Core.Interfaces.TokenValidationResult
            {
                IsValid = true,
                Claims = principal.Claims,
                JwtId = jwtId,
                UserId = tokenEntity.UserId,
                TenantId = tokenEntity.TenantId,
                TokenType = tokenEntity.TokenType,
                ExpiresAt = tokenEntity.ExpiresAt,
                IsNearExpiry = tokenEntity.IsNearExpiry()
            };
        }
        catch (SecurityTokenExpiredException)
        {
            return new Core.Interfaces.TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token has expired",
                ErrorCode = "token_expired"
            };
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            return new Core.Interfaces.TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid token signature",
                ErrorCode = "invalid_signature"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證 Token 失敗: {Token}", token);
            return new Core.Interfaces.TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token validation failed",
                ErrorCode = "validation_failed"
            };
        }
    }

    /// <summary>
    /// 刷新 Access Token
    /// </summary>
    public async Task<RefreshTokenResult> RefreshAccessTokenAsync(
        string refreshToken,
        IEnumerable<string>? newScopes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHash = ComputeTokenHash(refreshToken);
            var refreshTokenEntity = await _tokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (refreshTokenEntity == null || !refreshTokenEntity.IsRefreshToken())
            {
                throw new SecurityTokenException("Invalid refresh token");
            }

            if (!refreshTokenEntity.IsValid())
            {
                throw new SecurityTokenException("Refresh token is not valid");
            }

            // 檢查黑名單
            var isBlacklisted = await IsTokenBlacklistedAsync(refreshTokenEntity.JwtId, cancellationToken);
            if (isBlacklisted)
            {
                throw new SecurityTokenException("Refresh token is blacklisted");
            }

            var user = await _userRepository.GetByIdAsync(refreshTokenEntity.UserId, cancellationToken);
            if (user == null || !user.IsActive())
            {
                throw new SecurityTokenException("User is not active");
            }

            // 生成新的 Access Token
            var scopes = newScopes ?? refreshTokenEntity.GetScopesArray();
            var newAccessToken = await GenerateAccessTokenAsync(
                refreshTokenEntity.UserId,
                refreshTokenEntity.TenantId,
                scopes: scopes,
                clientId: refreshTokenEntity.ClientId,
                cancellationToken: cancellationToken);

            // 關聯 Refresh Token
            var accessTokenEntity = await _tokenRepository.GetByIdAsync(newAccessToken.TokenId, cancellationToken);
            if (accessTokenEntity != null)
            {
                accessTokenEntity.RefreshTokenId = refreshTokenEntity.Id;
                await _tokenRepository.UpdateAsync(accessTokenEntity, cancellationToken);
                await _tokenRepository.SaveChangesAsync(cancellationToken);
            }

            // 更新 Refresh Token 使用資訊
            refreshTokenEntity.MarkAsUsed();
            await _tokenRepository.UpdateAsync(refreshTokenEntity, cancellationToken);

            var result = new RefreshTokenResult
            {
                AccessToken = newAccessToken,
                RefreshTokenRotated = false
            };

            // 如果啟用 Refresh Token 輪替
            if (_settings.EnableRefreshTokenRotation)
            {
                // 撤銷舊的 Refresh Token
                refreshTokenEntity.Revoke("Refresh token rotated");

                // 生成新的 Refresh Token
                var newRefreshToken = await GenerateRefreshTokenAsync(
                    refreshTokenEntity.UserId,
                    refreshTokenEntity.TenantId,
                    refreshTokenEntity.ClientId,
                    scopes,
                    cancellationToken: cancellationToken);

                result.RefreshToken = newRefreshToken;
                result.RefreshTokenRotated = true;
            }

            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已刷新 Access Token: {AccessTokenId}, 使用 Refresh Token: {RefreshTokenId}", 
                newAccessToken.TokenId, refreshTokenEntity.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新 Access Token 失敗: {RefreshToken}", refreshToken);
            throw;
        }
    }

    /// <summary>
    /// 撤銷 Token
    /// </summary>
    public async Task<bool> RevokeTokenAsync(
        string token,
        string reason,
        Guid? revokedByUserId = null,
        bool revokeAssociatedTokens = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHash = ComputeTokenHash(token);
            var tokenEntity = await _tokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (tokenEntity == null)
            {
                return false;
            }

            // 撤銷主要 Token
            tokenEntity.Revoke(reason, revokedByUserId);

            // 加入黑名單
            await BlacklistTokenAsync(tokenEntity, reason, BlacklistType.Revoked, revokedByUserId, cancellationToken: cancellationToken);

            // 撤銷關聯的 Token
            if (revokeAssociatedTokens)
            {
                if (tokenEntity.IsRefreshToken())
                {
                    // 撤銷所有使用此 Refresh Token 的 Access Token
                    var associatedTokens = await _tokenRepository.GetAssociatedTokensAsync(tokenEntity.Id, cancellationToken);
                    foreach (var associatedToken in associatedTokens)
                    {
                        associatedToken.Revoke($"Associated with revoked refresh token: {reason}", revokedByUserId);
                        await BlacklistTokenAsync(associatedToken, reason, BlacklistType.Revoked, revokedByUserId, cancellationToken: cancellationToken);
                    }
                }
                else if (tokenEntity.IsAccessToken() && tokenEntity.RefreshTokenId.HasValue)
                {
                    // 撤銷對應的 Refresh Token
                    var refreshToken = await _tokenRepository.GetByIdAsync(tokenEntity.RefreshTokenId.Value, cancellationToken);
                    if (refreshToken != null)
                    {
                        refreshToken.Revoke($"Associated access token revoked: {reason}", revokedByUserId);
                        await BlacklistTokenAsync(refreshToken, reason, BlacklistType.Revoked, revokedByUserId, cancellationToken: cancellationToken);
                    }
                }
            }

            await _tokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("已撤銷 Token: {TokenId}, 原因: {Reason}", tokenEntity.Id, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤銷 Token 失敗: {Token}, 原因: {Reason}", token, reason);
            throw;
        }
    }

    /// <summary>
    /// 撤銷使用者的所有 Token
    /// </summary>
    public async Task<int> RevokeUserTokensAsync(
        Guid userId,
        string reason,
        Guid? revokedByUserId = null,
        IEnumerable<Guid>? excludeTokenIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revokedCount = await _tokenRepository.RevokeUserTokensAsync(userId, reason, revokedByUserId, excludeTokenIds, cancellationToken);
            
            // 將使用者的所有 Token 加入黑名單
            await _blacklistRepository.BlacklistUserTokensAsync(userId, reason, BlacklistType.UserLocked, revokedByUserId, cancellationToken: cancellationToken);

            _logger.LogInformation("已撤銷使用者的所有 Token: {UserId}, 數量: {Count}, 原因: {Reason}", userId, revokedCount, reason);

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤銷使用者 Token 失敗: {UserId}, 原因: {Reason}", userId, reason);
            throw;
        }
    }

    /// <summary>
    /// 檢查 Token 是否在黑名單中
    /// </summary>
    public async Task<bool> IsTokenBlacklistedAsync(string jwtId, CancellationToken cancellationToken = default)
    {
        return await _blacklistRepository.IsBlacklistedAsync(jwtId, cancellationToken);
    }

    /// <summary>
    /// 將 Token 加入黑名單
    /// </summary>
    public async Task<bool> BlacklistTokenAsync(
        JwtToken token,
        string reason,
        BlacklistType blacklistType = BlacklistType.Revoked,
        Guid? blacklistedByUserId = null,
        bool isPermanent = false,
        TimeSpan? blacklistDuration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existingBlacklist = await _blacklistRepository.GetBlacklistItemAsync(token.JwtId, cancellationToken);
            if (existingBlacklist != null)
            {
                return false; // 已經在黑名單中
            }

            var blacklistItem = new TokenBlacklist
            {
                JwtId = token.JwtId,
                TokenHash = token.TokenValue,
                UserId = token.UserId,
                TenantId = token.TenantId,
                TokenType = token.TokenType,
                BlacklistedAt = DateTime.UtcNow,
                OriginalExpiresAt = token.ExpiresAt,
                Reason = reason,
                BlacklistedByUserId = blacklistedByUserId,
                Type = blacklistType,
                IsPermanent = isPermanent,
                BlacklistExpiresAt = isPermanent ? null : 
                    (blacklistDuration.HasValue ? DateTime.UtcNow.Add(blacklistDuration.Value) : token.ExpiresAt.AddDays(30))
            };

            await _blacklistRepository.AddAsync(blacklistItem, cancellationToken);
            await _blacklistRepository.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "將 Token 加入黑名單失敗: {JwtId}", token.JwtId);
            throw;
        }
    }

    /// <summary>
    /// 取得使用者的 Token 清單
    /// </summary>
    public async Task<IEnumerable<JwtToken>> GetUserTokensAsync(
        Guid userId,
        string? tokenType = null,
        bool includeExpired = false,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default)
    {
        var status = includeRevoked ? (TokenStatus?)null : TokenStatus.Active;
        return await _tokenRepository.GetUserTokensAsync(userId, tokenType, status, includeExpired, cancellationToken);
    }

    /// <summary>
    /// 清理過期的 Token 和黑名單項目
    /// </summary>
    public async Task<CleanupResult> CleanupExpiredTokensAsync(
        int olderThanDays = 30,
        CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // 清理過期的 Token
            result.TokensCleanedUp = await _tokenRepository.DeleteExpiredTokensAsync(olderThanDays, cancellationToken: cancellationToken);

            // 清理過期的黑名單項目
            result.BlacklistItemsCleanedUp = await _blacklistRepository.DeleteExpiredBlacklistAsync(olderThanDays, cancellationToken: cancellationToken);

            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("清理完成: Token={TokenCount}, Blacklist={BlacklistCount}, 耗時={Duration}ms",
                result.TokensCleanedUp, result.BlacklistItemsCleanedUp, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "清理過期 Token 失敗");
            throw;
        }
    }

    /// <summary>
    /// 取得 Token 統計資訊
    /// </summary>
    public async Task<TokenStatistics> GetTokenStatisticsAsync(
        Guid? userId = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await _tokenRepository.GetTokenStatisticsAsync(userId, tenantId, cancellationToken);
        
        // 加入黑名單統計
        var blacklistStats = await _blacklistRepository.GetBlacklistStatisticsAsync(cancellationToken);
        stats.BlacklistItems = blacklistStats.TotalBlacklistItems;

        return stats;
    }

    #region 私有方法

    /// <summary>
    /// 生成 JWT ID
    /// </summary>
    private static string GenerateJwtId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 生成 Refresh Token 值
    /// </summary>
    private static string GenerateRefreshTokenValue()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// 計算 Token 雜湊值
    /// </summary>
    private static string ComputeTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// 建立 Access Token Claims
    /// </summary>
    private static List<Claim> BuildAccessTokenClaims(
        User user,
        Guid tenantId,
        string jwtId,
        DateTime issuedAt,
        string? clientId,
        IEnumerable<string>? scopes,
        IEnumerable<Claim>? additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("tenant_id", tenantId.ToString()),
            new("username", user.Username),
            new("email", user.Email),
            new("name", user.GetFullName())
        };

        if (!string.IsNullOrEmpty(clientId))
        {
            claims.Add(new Claim("client_id", clientId));
        }

        if (scopes != null && scopes.Any())
        {
            claims.Add(new Claim("scope", string.Join(" ", scopes)));
        }

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        return claims;
    }

    /// <summary>
    /// 建立 ID Token Claims
    /// </summary>
    private static List<Claim> BuildIdTokenClaims(
        User user,
        Guid tenantId,
        string jwtId,
        DateTime issuedAt,
        DateTime? authTime,
        string? nonce,
        IEnumerable<Claim>? additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("tenant_id", tenantId.ToString()),
            new("preferred_username", user.Username),
            new("email", user.Email),
            new("email_verified", "true"),
            new("name", user.GetFullName()),
            new("given_name", user.FirstName),
            new("family_name", user.LastName)
        };

        if (authTime.HasValue)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.AuthTime, 
                new DateTimeOffset(authTime.Value).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        }

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
        }

        if (!string.IsNullOrEmpty(user.PreferredLanguage))
        {
            claims.Add(new Claim("locale", user.PreferredLanguage));
        }

        if (!string.IsNullOrEmpty(user.TimeZone))
        {
            claims.Add(new Claim("zoneinfo", user.TimeZone));
        }

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        return claims;
    }

    /// <summary>
    /// 取得簽章憑證
    /// </summary>
    private SigningCredentials GetSigningCredentials()
    {
        var key = GetSigningKey();
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// 取得簽章金鑰
    /// </summary>
    private SecurityKey GetSigningKey()
    {
        if (!string.IsNullOrEmpty(_settings.PrivateKey))
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(_settings.PrivateKey), out _);
            return new RsaSecurityKey(rsa);
        }
        
        // 如果沒有 RSA 私鑰，使用 HMAC (不建議用於生產環境)
        var key = Encoding.UTF8.GetBytes(_settings.SecretKey);
        return new SymmetricSecurityKey(key);
    }

    #endregion
}

/// <summary>
/// JWT Token 設定
/// </summary>
public class JwtTokenSettings
{
    /// <summary>
    /// 發行者
    /// </summary>
    public string Issuer { get; set; } = "EnterpriseIdentityServer";

    /// <summary>
    /// 接收者
    /// </summary>
    public string Audience { get; set; } = "EnterpriseIDS";

    /// <summary>
    /// 密鑰 (HMAC，不建議用於生產環境)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// RSA 私鑰 (Base64 編碼，建議用於生產環境)
    /// </summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// RSA 公鑰 (Base64 編碼)
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Access Token 有效期 (分鐘)
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh Token 有效期 (天)
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// ID Token 有效期 (分鐘)
    /// </summary>
    public int IdTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// 時鐘偏移容忍度 (分鐘)
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;

    /// <summary>
    /// 是否啟用 Refresh Token 輪替
    /// </summary>
    public bool EnableRefreshTokenRotation { get; set; } = true;
}