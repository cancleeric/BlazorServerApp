using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// Token 黑名單 Repository 實作
/// </summary>
public class TokenBlacklistRepository : ITokenBlacklistRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<TokenBlacklistRepository> _logger;

    public TokenBlacklistRepository(
        LocalIdentityDbContext context,
        ILogger<TokenBlacklistRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddToBlacklistAsync(string tokenId, string tokenHash, DateTime expiresAt,
        string clientId, string? subject, string? reason)
    {
        try
        {
            // 檢查是否已經在黑名單中
            var existing = await _context.TokenBlacklist
                .FirstOrDefaultAsync(t => t.TokenId == tokenId);

            if (existing != null)
            {
                _logger.LogDebug("Token {TokenId} is already in blacklist", tokenId);
                return;
            }

            var blacklistEntry = new TokenBlacklistEntity
            {
                TokenId = tokenId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                RevokedAt = DateTime.UtcNow,
                RevocationReason = reason,
                ClientId = clientId,
                Subject = subject
            };

            _context.TokenBlacklist.Add(blacklistEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Token {TokenId} added to blacklist for client {ClientId}", 
                tokenId, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding token {TokenId} to blacklist", tokenId);
            throw;
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string tokenId)
    {
        try
        {
            return await _context.TokenBlacklist
                .AnyAsync(t => t.TokenId == tokenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if token {TokenId} is revoked", tokenId);
            return false;
        }
    }

    public async Task<bool> IsTokenHashRevokedAsync(string tokenHash)
    {
        try
        {
            return await _context.TokenBlacklist
                .AnyAsync(t => t.TokenHash == tokenHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if token hash is revoked");
            return false;
        }
    }

    public async Task RevokeAllUserTokensAsync(string subject, string reason)
    {
        try
        {
            // 這個方法主要用於緊急情況，將來可以結合 JWT 過期時間來實現
            // 目前記錄日誌以便後續擴展
            _logger.LogWarning("Request to revoke all tokens for user {Subject}, reason: {Reason}", 
                subject, reason);

            // 實際實現需要配合 Token 服務來獲取當前有效的 Token
            // 這裡暫時留空，將在 Token Revocation Service 中實現
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all tokens for user {Subject}", subject);
            throw;
        }
    }

    public async Task RevokeAllClientTokensAsync(string clientId, string reason)
    {
        try
        {
            _logger.LogWarning("Request to revoke all tokens for client {ClientId}, reason: {Reason}", 
                clientId, reason);

            // 實際實現需要配合 Token 服務來獲取當前有效的 Token
            // 這裡暫時留空，將在 Token Revocation Service 中實現
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all tokens for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task CleanupExpiredAsync()
    {
        try
        {
            var expiredTokens = await _context.TokenBlacklist
                .Where(t => t.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredTokens.Any())
            {
                _context.TokenBlacklist.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} expired blacklist entries", 
                    expiredTokens.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired blacklist entries");
            throw;
        }
    }

    public async Task<int> GetBlacklistCountAsync()
    {
        try
        {
            return await _context.TokenBlacklist.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blacklist count");
            return 0;
        }
    }

    /// <summary>
    /// 計算 Token 的 SHA256 雜湊值
    /// </summary>
    public static string ComputeTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}