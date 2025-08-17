using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// MFA 備用碼儲存庫實作
/// 遵循儲存庫模式，負責 MFA 備用碼的資料存取操作
/// </summary>
public class MfaBackupCodeRepository : IMfaBackupCodeRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<MfaBackupCodeRepository> _logger;

    public MfaBackupCodeRepository(LocalIdentityDbContext context, ILogger<MfaBackupCodeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MfaBackupCodeEntity> CreateAsync(MfaBackupCodeEntity backupCode)
    {
        if (backupCode == null)
            throw new ArgumentNullException(nameof(backupCode));

        try
        {
            _context.MfaBackupCodes.Add(backupCode);
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Created backup code for user: {UserId}, batch: {BatchId}", 
                backupCode.UserId, backupCode.BatchId);
            
            return backupCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup code for user: {UserId}", backupCode.UserId);
            throw;
        }
    }

    public async Task<MfaBackupCodeEntity> UpdateAsync(MfaBackupCodeEntity backupCode)
    {
        if (backupCode == null)
            throw new ArgumentNullException(nameof(backupCode));

        try
        {
            _context.MfaBackupCodes.Update(backupCode);
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Updated backup code: {Id} for user: {UserId}", 
                backupCode.Id, backupCode.UserId);
            
            return backupCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update backup code: {Id}", backupCode.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ID cannot be null or empty", nameof(id));

        try
        {
            var backupCode = await _context.MfaBackupCodes.FindAsync(id);
            if (backupCode == null)
                return false;

            _context.MfaBackupCodes.Remove(backupCode);
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Deleted backup code: {Id} for user: {UserId}", 
                id, backupCode.UserId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup code: {Id}", id);
            throw;
        }
    }

    public async Task<MfaBackupCodeEntity?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        try
        {
            return await _context.MfaBackupCodes
                .Include(bc => bc.User)
                .FirstOrDefaultAsync(bc => bc.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup code by ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MfaBackupCodeEntity>> GetUserBackupCodesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Enumerable.Empty<MfaBackupCodeEntity>();

        try
        {
            return await _context.MfaBackupCodes
                .Where(bc => bc.UserId == userId)
                .OrderByDescending(bc => bc.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup codes for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<MfaBackupCodeEntity>> GetAvailableBackupCodesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Enumerable.Empty<MfaBackupCodeEntity>();

        try
        {
            return await _context.MfaBackupCodes
                .Where(bc => bc.UserId == userId && 
                           !bc.IsUsed && 
                           bc.ExpiresAt > DateTime.UtcNow)
                .OrderBy(bc => bc.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available backup codes for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetAvailableBackupCodesCountAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        try
        {
            return await _context.MfaBackupCodes
                .CountAsync(bc => bc.UserId == userId && 
                                !bc.IsUsed && 
                                bc.ExpiresAt > DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available backup codes count for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<MfaBackupCodeEntity>> GetBackupCodesByBatchAsync(string batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId))
            return Enumerable.Empty<MfaBackupCodeEntity>();

        try
        {
            return await _context.MfaBackupCodes
                .Where(bc => bc.BatchId == batchId)
                .Include(bc => bc.User)
                .OrderBy(bc => bc.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup codes by batch: {BatchId}", batchId);
            throw;
        }
    }

    public async Task<IEnumerable<MfaBackupCodeEntity>> GetExpiredBackupCodesAsync()
    {
        try
        {
            return await _context.MfaBackupCodes
                .Where(bc => !bc.IsUsed && bc.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get expired backup codes");
            throw;
        }
    }

    public async Task<int> RevokeAllUserBackupCodesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        try
        {
            var availableCodes = await _context.MfaBackupCodes
                .Where(bc => bc.UserId == userId && 
                           !bc.IsUsed && 
                           bc.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var code in availableCodes)
            {
                code.IsUsed = true;
                code.UsedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Revoked {Count} backup codes for user: {UserId}", 
                availableCodes.Count, userId);
            
            return availableCodes.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke backup codes for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<int> CleanupExpiredBackupCodesAsync(DateTime beforeDate)
    {
        try
        {
            var expiredCodes = await _context.MfaBackupCodes
                .Where(bc => bc.ExpiresAt <= beforeDate)
                .ToListAsync();

            _context.MfaBackupCodes.RemoveRange(expiredCodes);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Cleaned up {Count} expired backup codes", expiredCodes.Count);
            
            return expiredCodes.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired backup codes");
            throw;
        }
    }

    public async Task<BackupCodeStatistics> GetBackupCodeStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _context.MfaBackupCodes.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(bc => bc.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(bc => bc.CreatedAt <= endDate.Value);

            var codes = await query.ToListAsync();
            var now = DateTime.UtcNow;

            return new BackupCodeStatistics
            {
                TotalGenerated = codes.Count,
                TotalUsed = codes.Count(bc => bc.IsUsed),
                TotalExpired = codes.Count(bc => !bc.IsUsed && bc.ExpiresAt <= now),
                TotalAvailable = codes.Count(bc => !bc.IsUsed && bc.ExpiresAt > now),
                UsersWithBackupCodes = codes.Select(bc => bc.UserId).Distinct().Count(),
                EarliestCreated = codes.Any() ? codes.Min(bc => bc.CreatedAt) : null,
                LatestCreated = codes.Any() ? codes.Max(bc => bc.CreatedAt) : null,
                LatestUsed = codes.Where(bc => bc.UsedAt.HasValue).Any() ? 
                           codes.Where(bc => bc.UsedAt.HasValue).Max(bc => bc.UsedAt) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup code statistics");
            throw;
        }
    }
}