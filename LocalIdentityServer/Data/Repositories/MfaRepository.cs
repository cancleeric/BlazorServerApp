using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// MFA 方法儲存庫實作
/// 遵循儲存庫模式，負責使用者 MFA 方法的資料存取操作
/// </summary>
public class MfaRepository : IMfaRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<MfaRepository> _logger;

    public MfaRepository(LocalIdentityDbContext context, ILogger<MfaRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserMfaEntity> CreateAsync(UserMfaEntity mfaMethod)
    {
        if (mfaMethod == null)
            throw new ArgumentNullException(nameof(mfaMethod));

        try
        {
            _context.UserMfaMethods.Add(mfaMethod);
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Created MFA method for user: {UserId}, method: {Method}", 
                mfaMethod.UserId, mfaMethod.Method);
            
            return mfaMethod;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MFA method for user: {UserId}", mfaMethod.UserId);
            throw;
        }
    }

    public async Task<UserMfaEntity> UpdateAsync(UserMfaEntity mfaMethod)
    {
        if (mfaMethod == null)
            throw new ArgumentNullException(nameof(mfaMethod));

        try
        {
            mfaMethod.UpdatedAt = DateTime.UtcNow;
            _context.UserMfaMethods.Update(mfaMethod);
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Updated MFA method: {Id} for user: {UserId}", 
                mfaMethod.Id, mfaMethod.UserId);
            
            return mfaMethod;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MFA method: {Id}", mfaMethod.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ID cannot be null or empty", nameof(id));

        try
        {
            var mfaMethod = await _context.UserMfaMethods.FindAsync(id);
            if (mfaMethod == null)
                return false;

            _context.UserMfaMethods.Remove(mfaMethod);
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Deleted MFA method: {Id} for user: {UserId}", 
                id, mfaMethod.UserId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete MFA method: {Id}", id);
            throw;
        }
    }

    public async Task<UserMfaEntity?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        try
        {
            return await _context.UserMfaMethods
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA method by ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<UserMfaEntity>> GetByUserIdAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Enumerable.Empty<UserMfaEntity>();

        try
        {
            return await _context.UserMfaMethods
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.Method)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA methods for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<UserMfaEntity?> GetByUserIdAndMethodAsync(string userId, string method)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(method))
            return null;

        try
        {
            return await _context.UserMfaMethods
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Method == method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA method for user: {UserId}, method: {Method}", 
                userId, method);
            throw;
        }
    }

    public async Task<UserMfaEntity?> GetPrimaryMfaMethodAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        try
        {
            return await _context.UserMfaMethods
                .Where(m => m.UserId == userId && m.IsEnabled && m.IsPrimary)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get primary MFA method for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<UserMfaEntity>> GetEnabledMfaMethodsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Enumerable.Empty<UserMfaEntity>();

        try
        {
            return await _context.UserMfaMethods
                .Where(m => m.UserId == userId && m.IsEnabled)
                .Where(m => m.LockedUntil == null || m.LockedUntil <= DateTime.UtcNow)
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.Method)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get enabled MFA methods for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> HasEnabledMfaAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        try
        {
            return await _context.UserMfaMethods
                .AnyAsync(m => m.UserId == userId && m.IsEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if user has enabled MFA: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<UserMfaEntity>> GetLockedMfaMethodsAsync(string? userId = null)
    {
        try
        {
            var query = _context.UserMfaMethods
                .Where(m => m.LockedUntil != null && m.LockedUntil > DateTime.UtcNow);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(m => m.UserId == userId);
            }

            return await query
                .Include(m => m.User)
                .OrderBy(m => m.LockedUntil)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get locked MFA methods");
            throw;
        }
    }

    public async Task<int> UnlockExpiredMfaMethodsAsync()
    {
        try
        {
            var expiredLocks = await _context.UserMfaMethods
                .Where(m => m.LockedUntil != null && m.LockedUntil <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var method in expiredLocks)
            {
                method.LockedUntil = null;
                method.FailedAttempts = 0;
                method.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Unlocked {Count} expired MFA methods", expiredLocks.Count);
            
            return expiredLocks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock expired MFA methods");
            throw;
        }
    }

    public async Task<MfaStatistics> GetMfaStatisticsAsync()
    {
        try
        {
            var mfaMethods = await _context.UserMfaMethods
                .Where(m => m.IsEnabled)
                .ToListAsync();

            var userIds = mfaMethods.Select(m => m.UserId).Distinct().ToList();
            var lockedMethods = mfaMethods.Where(m => m.LockedUntil != null && m.LockedUntil > DateTime.UtcNow);

            var methodCounts = mfaMethods
                .GroupBy(m => m.Method)
                .ToDictionary(g => g.Key, g => g.Count());

            return new MfaStatistics
            {
                TotalMfaUsers = userIds.Count,
                TotpUsers = methodCounts.GetValueOrDefault("TOTP", 0),
                SmsUsers = methodCounts.GetValueOrDefault("SMS", 0),
                EmailUsers = methodCounts.GetValueOrDefault("Email", 0),
                LockedMethods = lockedMethods.Count(),
                MethodCounts = methodCounts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA statistics");
            throw;
        }
    }
}