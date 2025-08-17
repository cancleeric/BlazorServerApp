using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Infrastructure.Data.Repositories;

/// <summary>
/// Token 黑名單 Repository 實作
/// </summary>
public class TokenBlacklistRepository : BaseRepository<TokenBlacklist>, ITokenBlacklistRepository
{
    public TokenBlacklistRepository(EnterpriseIdentityDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根據 JWT ID 檢查是否在黑名單
    /// </summary>
    public async Task<bool> IsBlacklistedAsync(string jwtId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(b => b.JwtId == jwtId && 
                          !b.IsDeleted && 
                          b.IsActive(), 
                     cancellationToken);
    }

    /// <summary>
    /// 根據 Token 雜湊檢查是否在黑名單
    /// </summary>
    public async Task<bool> IsBlacklistedByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(b => b.TokenHash == tokenHash && 
                          !b.IsDeleted && 
                          b.IsActive(), 
                     cancellationToken);
    }

    /// <summary>
    /// 取得黑名單項目
    /// </summary>
    public async Task<TokenBlacklist?> GetBlacklistItemAsync(string jwtId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.User)
            .Include(b => b.Tenant)
            .Include(b => b.BlacklistedByUser)
            .FirstOrDefaultAsync(b => b.JwtId == jwtId && !b.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// 取得使用者的黑名單項目
    /// </summary>
    public async Task<IEnumerable<TokenBlacklist>> GetUserBlacklistAsync(
        Guid userId,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(b => b.Tenant)
            .Include(b => b.BlacklistedByUser)
            .Where(b => b.UserId == userId && !b.IsDeleted);

        if (!includeExpired)
        {
            query = query.Where(b => b.IsActive());
        }

        return await query
            .OrderByDescending(b => b.BlacklistedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得可清理的黑名單項目
    /// </summary>
    public async Task<IEnumerable<TokenBlacklist>> GetCleanupCandidatesAsync(
        int olderThanDays = 30,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddDays(-olderThanDays);

        return await _dbSet
            .Where(b => !b.IsDeleted && 
                       b.CanBeCleanedUp() &&
                       b.OriginalExpiresAt < cutoffTime)
            .OrderBy(b => b.OriginalExpiresAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 批次刪除過期的黑名單項目
    /// </summary>
    public async Task<int> DeleteExpiredBlacklistAsync(
        int olderThanDays = 30,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddDays(-olderThanDays);
        var totalDeleted = 0;

        while (true)
        {
            var expiredItems = await _dbSet
                .Where(b => !b.IsDeleted &&
                           b.OriginalExpiresAt < cutoffTime &&
                           (!b.IsPermanent && 
                            (!b.BlacklistExpiresAt.HasValue || b.BlacklistExpiresAt.Value < DateTime.UtcNow)))
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (!expiredItems.Any())
                break;

            _dbSet.RemoveRange(expiredItems);
            await _context.SaveChangesAsync(cancellationToken);
            
            totalDeleted += expiredItems.Count;

            // 如果這批次少於批次大小，表示已經處理完畢
            if (expiredItems.Count < batchSize)
                break;
        }

        return totalDeleted;
    }

    /// <summary>
    /// 將使用者的所有 Token 加入黑名單
    /// </summary>
    public async Task<int> BlacklistUserTokensAsync(
        Guid userId,
        string reason,
        BlacklistType blacklistType = BlacklistType.UserLocked,
        Guid? blacklistedByUserId = null,
        bool isPermanent = false,
        CancellationToken cancellationToken = default)
    {
        // 首先取得使用者的所有有效 Token
        var userTokens = await _context.Set<JwtToken>()
            .Where(t => t.UserId == userId && 
                       !t.IsDeleted && 
                       t.Status == TokenStatus.Active &&
                       t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var blacklistItems = new List<TokenBlacklist>();
        var now = DateTime.UtcNow;

        foreach (var token in userTokens)
        {
            // 檢查是否已經在黑名單中
            var existingBlacklist = await _dbSet
                .FirstOrDefaultAsync(b => b.JwtId == token.JwtId && !b.IsDeleted, cancellationToken);

            if (existingBlacklist == null)
            {
                var blacklistItem = new TokenBlacklist
                {
                    JwtId = token.JwtId,
                    TokenHash = JwtTokenRepository.ComputeTokenHash(token.TokenValue),
                    UserId = userId,
                    TenantId = token.TenantId,
                    TokenType = token.TokenType,
                    BlacklistedAt = now,
                    OriginalExpiresAt = token.ExpiresAt,
                    Reason = reason,
                    BlacklistedByUserId = blacklistedByUserId,
                    Type = blacklistType,
                    IsPermanent = isPermanent,
                    BlacklistExpiresAt = isPermanent ? null : token.ExpiresAt.AddDays(30) // 延長 30 天
                };

                blacklistItems.Add(blacklistItem);
            }

            // 撤銷 Token
            token.Revoke(reason, blacklistedByUserId);
        }

        if (blacklistItems.Any())
        {
            await _dbSet.AddRangeAsync(blacklistItems, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return blacklistItems.Count;
    }

    /// <summary>
    /// 取得黑名單統計資訊
    /// </summary>
    public async Task<BlacklistStatistics> GetBlacklistStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var stats = new BlacklistStatistics
        {
            TotalBlacklistItems = await _dbSet.CountAsync(b => !b.IsDeleted, cancellationToken),
            ActiveBlacklistItems = await _dbSet.CountAsync(b => !b.IsDeleted && b.IsActive(), cancellationToken),
            ExpiredBlacklistItems = await _dbSet.CountAsync(b => !b.IsDeleted && !b.IsActive(), cancellationToken),
            PermanentBlacklistItems = await _dbSet.CountAsync(b => !b.IsDeleted && b.IsPermanent, cancellationToken),
            LatestBlacklistAt = await _dbSet
                .Where(b => !b.IsDeleted)
                .MaxAsync(b => (DateTime?)b.BlacklistedAt, cancellationToken)
        };

        // 各類型統計
        var typeGroups = await _dbSet
            .Where(b => !b.IsDeleted)
            .GroupBy(b => b.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var group in typeGroups)
        {
            stats.CountByType[group.Type] = group.Count;
        }

        return stats;
    }
}