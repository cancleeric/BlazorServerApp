using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 刷新權杖 Repository 實作 - 遵循 SOLID 原則
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<RefreshTokenRepository> _logger;

    public RefreshTokenRepository(LocalIdentityDbContext context, ILogger<RefreshTokenRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RefreshTokenEntity> StoreAsync(RefreshTokenEntity refreshToken)
    {
        try
        {
            if (refreshToken == null)
            {
                throw new ArgumentNullException(nameof(refreshToken));
            }

            _logger.LogDebug("Storing refresh token for client: {ClientId}, subject: {Subject}", 
                refreshToken.ClientId, refreshToken.Subject);

            // 如果存在相同的refresh token，先檢查是否有衝突
            var existingToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken.Token);

            if (existingToken != null && !existingToken.IsUsed && !existingToken.IsRevoked)
            {
                throw new InvalidOperationException($"Refresh token already exists and is still active");
            }

            refreshToken.CreatedAt = DateTime.UtcNow;
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token stored successfully for client: {ClientId}", refreshToken.ClientId);
            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing refresh token for client: {ClientId}", refreshToken?.ClientId);
            throw;
        }
    }

    public async Task<RefreshTokenEntity> CreateAsync(RefreshTokenEntity refreshToken)
    {
        // CreateAsync 是 StoreAsync 的別名方法
        return await StoreAsync(refreshToken);
    }

    public async Task<RefreshTokenEntity> UpdateAsync(RefreshTokenEntity refreshToken)
    {
        try
        {
            _logger.LogDebug("Updating refresh token for client: {ClientId}", refreshToken.ClientId);

            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token updated successfully for client: {ClientId}", refreshToken.ClientId);
            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating refresh token");
            throw;
        }
    }

    public async Task<RefreshTokenEntity?> GetByTokenAsync(string token)
    {
        try
        {
            _logger.LogDebug("Retrieving refresh token");

            var refreshToken = await _context.RefreshTokens
                .Include(rt => rt.Client)
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsUsed && !rt.IsRevoked);

            if (refreshToken != null)
            {
                _logger.LogDebug("Refresh token found for client: {ClientId}", refreshToken.ClientId);
            }
            else
            {
                _logger.LogDebug("Refresh token not found or invalid");
            }

            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving refresh token");
            throw;
        }
    }

    public async Task<RefreshTokenEntity?> UseTokenAsync(string token, string? replacedByToken = null)
    {
        try
        {
            _logger.LogDebug("Marking refresh token as used");

            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
            {
                throw new InvalidOperationException("Refresh token not found");
            }

            refreshToken.IsUsed = true;
            refreshToken.UsedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(replacedByToken))
            {
                refreshToken.ReplacedByToken = replacedByToken;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token marked as used for client: {ClientId}", refreshToken.ClientId);
            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking refresh token as used");
            throw;
        }
    }

    public async Task RevokeTokenAsync(string token, string reason)
    {
        try
        {
            _logger.LogDebug("Revoking refresh token");

            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
            {
                _logger.LogWarning("Refresh token not found for revocation");
                return;
            }

            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokeReason = reason;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token revoked for client: {ClientId}", refreshToken.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token");
            throw;
        }
    }

    public async Task RevokeTokenChainAsync(string token, string reason)
    {
        try
        {
            _logger.LogDebug("Revoking refresh token chain");

            // 找到當前權杖
            var currentToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (currentToken == null)
            {
                _logger.LogWarning("Current token not found for chain revocation");
                return;
            }

            // 撤銷當前權杖
            currentToken.IsRevoked = true;
            currentToken.RevokedAt = DateTime.UtcNow;
            currentToken.RevokeReason = reason;

            // 找到所有被這個權杖取代的權杖（鏈）
            var relatedTokens = await _context.RefreshTokens
                .Where(rt => rt.Subject == currentToken.Subject && 
                            rt.ClientId == currentToken.ClientId && 
                            !rt.IsRevoked)
                .ToListAsync();

            foreach (var relatedToken in relatedTokens)
            {
                relatedToken.IsRevoked = true;
                relatedToken.RevokedAt = DateTime.UtcNow;
                relatedToken.RevokeReason = reason;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token chain revoked. Affected tokens: {Count}", relatedTokens.Count + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token chain");
            throw;
        }
    }

    public async Task RevokeTokenFamilyAsync(string tokenFamily, string reason)
    {
        try
        {
            _logger.LogDebug("Revoking entire token family: {TokenFamily}", tokenFamily);

            var familyTokens = await _context.RefreshTokens
                .Where(rt => rt.TokenFamily == tokenFamily && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in familyTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokeReason = reason;
            }

            await _context.SaveChangesAsync();

            _logger.LogWarning("Token family revoked due to security concern. Family: {TokenFamily}, Affected tokens: {Count}", 
                tokenFamily, familyTokens.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token family: {TokenFamily}", tokenFamily);
            throw;
        }
    }

    public async Task<List<RefreshTokenEntity>> GetByTokenFamilyAsync(string tokenFamily)
    {
        try
        {
            _logger.LogDebug("Retrieving tokens by family: {TokenFamily}", tokenFamily);

            return await _context.RefreshTokens
                .Where(rt => rt.TokenFamily == tokenFamily)
                .OrderBy(rt => rt.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tokens by family: {TokenFamily}", tokenFamily);
            throw;
        }
    }

    public async Task<bool> IsTokenReusedAsync(string token)
    {
        try
        {
            _logger.LogDebug("Checking if token is reused: {TokenPrefix}", token.Substring(0, Math.Min(8, token.Length)));

            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
            {
                return false;
            }

            // Token 被重用的條件：已使用且有人嘗試再次使用
            return refreshToken.IsUsed || refreshToken.IsRevoked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token reuse");
            throw;
        }
    }

    public async Task CleanupExpiredAsync()
    {
        try
        {
            _logger.LogDebug("Cleaning up expired refresh tokens");

            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredTokens.Any())
            {
                _context.RefreshTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} expired refresh tokens", expiredTokens.Count);
            }
            else
            {
                _logger.LogDebug("No expired refresh tokens found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired refresh tokens");
            throw;
        }
    }

    public async Task RevokeByUserAsync(string subject, string reason)
    {
        try
        {
            _logger.LogDebug("Revoking all refresh tokens for user: {Subject}", subject);

            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.Subject == subject && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokeReason = reason;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Revoked {Count} refresh tokens for user: {Subject}", userTokens.Count, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh tokens for user: {Subject}", subject);
            throw;
        }
    }

    public async Task RevokeByClientAsync(string clientId, string reason)
    {
        try
        {
            _logger.LogDebug("Revoking all refresh tokens for client: {ClientId}", clientId);

            var clientTokens = await _context.RefreshTokens
                .Where(rt => rt.ClientId == clientId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in clientTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokeReason = reason;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Revoked {Count} refresh tokens for client: {ClientId}", clientTokens.Count, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh tokens for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<bool> IsValidTokenAsync(string token)
    {
        try
        {
            _logger.LogDebug("Validating refresh token");

            var refreshToken = await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
            {
                _logger.LogDebug("Refresh token not found");
                return false;
            }

            var isValid = !refreshToken.IsUsed && 
                         !refreshToken.IsRevoked && 
                         refreshToken.ExpiresAt > DateTime.UtcNow;

            _logger.LogDebug("Refresh token validation result: {IsValid}", isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating refresh token");
            return false;
        }
    }
}
