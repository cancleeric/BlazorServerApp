using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace EnterpriseIDS.Infrastructure.Data.Repositories;

/// <summary>
/// JWT Token Repository 實作
/// </summary>
public class JwtTokenRepository : BaseRepository<JwtToken>, IJwtTokenRepository
{
    public JwtTokenRepository(EnterpriseIdentityDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根據 JWT ID 取得 Token
    /// </summary>
    public async Task<JwtToken?> GetByJwtIdAsync(string jwtId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.User)
            .Include(t => t.Tenant)
            .Include(t => t.RefreshToken)
            .Include(t => t.ParentToken)
            .FirstOrDefaultAsync(t => t.JwtId == jwtId && !t.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// 根據 Token 值雜湊取得 Token
    /// </summary>
    public async Task<JwtToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.User)
            .Include(t => t.Tenant)
            .FirstOrDefaultAsync(t => t.TokenValue == tokenHash && !t.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// 取得使用者的 Token 清單
    /// </summary>
    public async Task<IEnumerable<JwtToken>> GetUserTokensAsync(
        Guid userId,
        string? tokenType = null,
        TokenStatus? status = null,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(t => t.Tenant)
            .Where(t => t.UserId == userId && !t.IsDeleted);

        if (!string.IsNullOrEmpty(tokenType))
        {
            query = query.Where(t => t.TokenType == tokenType);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (!includeExpired)
        {
            query = query.Where(t => t.ExpiresAt > DateTime.UtcNow);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得租戶的 Token 清單
    /// </summary>
    public async Task<IEnumerable<JwtToken>> GetTenantTokensAsync(
        Guid tenantId,
        string? tokenType = null,
        TokenStatus? status = null,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(t => t.User)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        if (!string.IsNullOrEmpty(tokenType))
        {
            query = query.Where(t => t.TokenType == tokenType);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (!includeExpired)
        {
            query = query.Where(t => t.ExpiresAt > DateTime.UtcNow);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得即將過期的 Token
    /// </summary>
    public async Task<IEnumerable<JwtToken>> GetExpiringTokensAsync(
        int withinMinutes = 30,
        string? tokenType = null,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(withinMinutes);
        
        var query = _dbSet
            .Include(t => t.User)
            .Include(t => t.Tenant)
            .Where(t => !t.IsDeleted && 
                       t.Status == TokenStatus.Active &&
                       t.ExpiresAt <= cutoffTime &&
                       t.ExpiresAt > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(tokenType))
        {
            query = query.Where(t => t.TokenType == tokenType);
        }

        return await query
            .OrderBy(t => t.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得已過期的 Token
    /// </summary>
    public async Task<IEnumerable<JwtToken>> GetExpiredTokensAsync(
        int olderThanDays = 1,
        string? tokenType = null,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddDays(-olderThanDays);
        
        var query = _dbSet
            .Where(t => !t.IsDeleted && t.ExpiresAt < cutoffTime);

        if (!string.IsNullOrEmpty(tokenType))
        {
            query = query.Where(t => t.TokenType == tokenType);
        }

        return await query
            .OrderBy(t => t.ExpiresAt)
            .ToListAsync(cancellationToken);
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
        var query = _dbSet
            .Where(t => t.UserId == userId && 
                       !t.IsDeleted && 
                       t.Status == TokenStatus.Active);

        if (excludeTokenIds != null && excludeTokenIds.Any())
        {
            var excludeIds = excludeTokenIds.ToList();
            query = query.Where(t => !excludeIds.Contains(t.Id));
        }

        var tokens = await query.ToListAsync(cancellationToken);
        
        foreach (var token in tokens)
        {
            token.Revoke(reason, revokedByUserId);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }

    /// <summary>
    /// 撤銷租戶的所有 Token
    /// </summary>
    public async Task<int> RevokeTenantTokensAsync(
        Guid tenantId,
        string reason,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _dbSet
            .Where(t => t.TenantId == tenantId && 
                       !t.IsDeleted && 
                       t.Status == TokenStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(reason, revokedByUserId);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }

    /// <summary>
    /// 批次刪除過期的 Token
    /// </summary>
    public async Task<int> DeleteExpiredTokensAsync(
        int olderThanDays = 30,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddDays(-olderThanDays);
        var totalDeleted = 0;

        while (true)
        {
            var expiredTokens = await _dbSet
                .Where(t => t.ExpiresAt < cutoffTime)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (!expiredTokens.Any())
                break;

            _dbSet.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync(cancellationToken);
            
            totalDeleted += expiredTokens.Count;

            // 如果這批次少於批次大小，表示已經處理完畢
            if (expiredTokens.Count < batchSize)
                break;
        }

        return totalDeleted;
    }

    /// <summary>
    /// 取得 Token 統計資訊
    /// </summary>
    public async Task<TokenStatistics> GetTokenStatisticsAsync(
        Guid? userId = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(t => !t.IsDeleted);

        if (userId.HasValue)
        {
            query = query.Where(t => t.UserId == userId.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(t => t.TenantId == tenantId.Value);
        }

        var now = DateTime.UtcNow;

        var stats = new TokenStatistics
        {
            TotalTokens = await query.CountAsync(cancellationToken),
            ActiveTokens = await query.CountAsync(t => t.Status == TokenStatus.Active && t.ExpiresAt > now, cancellationToken),
            ExpiredTokens = await query.CountAsync(t => t.ExpiresAt <= now, cancellationToken),
            RevokedTokens = await query.CountAsync(t => t.Status == TokenStatus.Revoked, cancellationToken),
            AccessTokens = await query.CountAsync(t => t.TokenType == "access_token", cancellationToken),
            RefreshTokens = await query.CountAsync(t => t.TokenType == "refresh_token", cancellationToken),
            IdTokens = await query.CountAsync(t => t.TokenType == "id_token", cancellationToken),
            LatestTokenIssuedAt = await query.MaxAsync(t => (DateTime?)t.IssuedAt, cancellationToken)
        };

        return stats;
    }

    /// <summary>
    /// 檢查使用者是否有有效的 Token
    /// </summary>
    public async Task<bool> HasValidTokenAsync(
        Guid userId,
        string? tokenType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Where(t => t.UserId == userId && 
                       !t.IsDeleted && 
                       t.Status == TokenStatus.Active && 
                       t.ExpiresAt > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(tokenType))
        {
            query = query.Where(t => t.TokenType == tokenType);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// 取得使用者的活躍會話數量
    /// </summary>
    public async Task<int> GetActiveSessionCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 計算有效的 Refresh Token 數量作為活躍會話
        return await _dbSet
            .CountAsync(t => t.UserId == userId && 
                           !t.IsDeleted && 
                           t.TokenType == "refresh_token" &&
                           t.Status == TokenStatus.Active && 
                           t.ExpiresAt > DateTime.UtcNow, 
                       cancellationToken);
    }

    /// <summary>
    /// 取得關聯的 Token
    /// </summary>
    public async Task<IEnumerable<JwtToken>> GetAssociatedTokensAsync(
        Guid refreshTokenId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.User)
            .Include(t => t.Tenant)
            .Where(t => t.RefreshTokenId == refreshTokenId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 更新 Token 使用資訊
    /// </summary>
    public async Task<bool> UpdateTokenUsageAsync(
        string jwtId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var token = await _dbSet
            .FirstOrDefaultAsync(t => t.JwtId == jwtId && !t.IsDeleted, cancellationToken);

        if (token == null)
            return false;

        token.MarkAsUsed(ipAddress, userAgent);
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    /// <summary>
    /// 計算 Token 雜湊值
    /// </summary>
    public static string ComputeTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}