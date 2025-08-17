using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 持久化金鑰 Repository 實作 - 遵循 SOLID 原則
/// </summary>
public class PersistedKeyRepository : IPersistedKeyRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<PersistedKeyRepository> _logger;

    public PersistedKeyRepository(LocalIdentityDbContext context, ILogger<PersistedKeyRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PersistedKeyEntity?> GetByKeyIdAsync(string keyId)
    {
        try
        {
            _logger.LogDebug("Retrieving persisted key: {KeyId}", keyId);

            var key = await _context.PersistedKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.KeyId == keyId && !k.IsRevoked);

            if (key != null)
            {
                _logger.LogDebug("Persisted key found: {KeyId}", keyId);
            }
            else
            {
                _logger.LogDebug("Persisted key not found: {KeyId}", keyId);
            }

            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving persisted key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<PersistedKeyEntity?> GetPrimarySigningKeyAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving primary signing key");

            var primaryKey = await _context.PersistedKeys
                .AsNoTracking()
                .Where(k => k.Use == "sig" && 
                           k.IsPrimary && 
                           !k.IsRevoked &&
                           k.ActivatedAt <= DateTime.UtcNow &&
                           (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow))
                .OrderByDescending(k => k.ActivatedAt)
                .FirstOrDefaultAsync();

            if (primaryKey != null)
            {
                _logger.LogDebug("Primary signing key found: {KeyId}", primaryKey.KeyId);
            }
            else
            {
                _logger.LogWarning("No primary signing key found");
            }

            return primaryKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving primary signing key");
            throw;
        }
    }

    public async Task<IEnumerable<PersistedKeyEntity>> GetValidSigningKeysAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving all valid signing keys");

            var validKeys = await _context.PersistedKeys
                .AsNoTracking()
                .Where(k => k.Use == "sig" && 
                           !k.IsRevoked &&
                           k.ActivatedAt <= DateTime.UtcNow &&
                           (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow))
                .OrderByDescending(k => k.IsPrimary)
                .ThenByDescending(k => k.ActivatedAt)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} valid signing keys", validKeys.Count);
            return validKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving valid signing keys");
            throw;
        }
    }

    public async Task<PersistedKeyEntity> StoreAsync(PersistedKeyEntity key)
    {
        try
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _logger.LogInformation("Storing persisted key: {KeyId}", key.KeyId);

            // 檢查是否已存在相同的 KeyId
            var existingKey = await _context.PersistedKeys
                .FirstOrDefaultAsync(k => k.KeyId == key.KeyId);

            if (existingKey != null)
            {
                throw new InvalidOperationException($"Key with ID '{key.KeyId}' already exists");
            }

            key.CreatedAt = DateTime.UtcNow;
            key.ActivatedAt = DateTime.UtcNow;

            _context.PersistedKeys.Add(key);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Persisted key stored successfully: {KeyId}", key.KeyId);
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing persisted key: {KeyId}", key?.KeyId);
            throw;
        }
    }

    public async Task SetPrimaryKeyAsync(string keyId)
    {
        try
        {
            _logger.LogInformation("Setting primary key: {KeyId}", keyId);

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 清除所有現有的主要金鑰標記
                var currentPrimaryKeys = await _context.PersistedKeys
                    .Where(k => k.IsPrimary && k.Use == "sig")
                    .ToListAsync();

                foreach (var currentKey in currentPrimaryKeys)
                {
                    currentKey.IsPrimary = false;
                }

                // 設定新的主要金鑰
                var newPrimaryKey = await _context.PersistedKeys
                    .FirstOrDefaultAsync(k => k.KeyId == keyId && k.Use == "sig");

                if (newPrimaryKey == null)
                {
                    throw new InvalidOperationException($"Signing key with ID '{keyId}' not found");
                }

                newPrimaryKey.IsPrimary = true;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Primary key set successfully: {KeyId}", keyId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task RevokeKeyAsync(string keyId)
    {
        try
        {
            _logger.LogInformation("Revoking key: {KeyId}", keyId);

            var key = await _context.PersistedKeys
                .FirstOrDefaultAsync(k => k.KeyId == keyId);

            if (key == null)
            {
                throw new InvalidOperationException($"Key with ID '{keyId}' not found");
            }

            key.IsRevoked = true;
            key.RevokedAt = DateTime.UtcNow;

            // 如果撤銷的是主要金鑰，需要選擇新的主要金鑰
            if (key.IsPrimary && key.Use == "sig")
            {
                key.IsPrimary = false;

                // 嘗試找到下一個有效的簽章金鑰作為主要金鑰
                var nextPrimaryKey = await _context.PersistedKeys
                    .Where(k => k.Use == "sig" && 
                               !k.IsRevoked && 
                               k.KeyId != keyId &&
                               k.ActivatedAt <= DateTime.UtcNow &&
                               (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow))
                    .OrderByDescending(k => k.ActivatedAt)
                    .FirstOrDefaultAsync();

                if (nextPrimaryKey != null)
                {
                    nextPrimaryKey.IsPrimary = true;
                    _logger.LogInformation("New primary key set: {NewKeyId}", nextPrimaryKey.KeyId);
                }
                else
                {
                    _logger.LogWarning("No valid signing key available to set as primary after revoking: {KeyId}", keyId);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Key revoked successfully: {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task SoftDeleteKeyAsync(string keyId)
    {
        try
        {
            _logger.LogDebug("Soft deleting key: {KeyId}", keyId);

            var key = await _context.PersistedKeys
                .FirstOrDefaultAsync(k => k.KeyId == keyId);

            if (key == null)
            {
                _logger.LogWarning("Key not found for soft deletion: {KeyId}", keyId);
                throw new InvalidOperationException($"Key with ID '{keyId}' not found");
            }

            // 軟刪除 - 標記為已刪除
            key.IsDeleted = true;
            key.DeletedAt = DateTime.UtcNow;
            key.LastModifiedAt = DateTime.UtcNow;

            // 如果是主要金鑰，取消主要狀態
            if (key.IsPrimary)
            {
                key.IsPrimary = false;
                _logger.LogWarning("Primary key {KeyId} was soft deleted, primary status removed", keyId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Key soft deleted successfully: {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<int> CleanupExpiredKeysAsync()
    {
        try
        {
            _logger.LogDebug("Cleaning up expired keys");

            var expiredKeys = await _context.PersistedKeys
                .Where(k => k.ExpiresAt != null && k.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredKeys.Any())
            {
                _context.PersistedKeys.RemoveRange(expiredKeys);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} expired keys", expiredKeys.Count);
                return expiredKeys.Count;
            }
            else
            {
                _logger.LogDebug("No expired keys found");
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired keys");
            throw;
        }
    }

    public async Task<bool> ShouldRotateKeyAsync()
    {
        try
        {
            _logger.LogDebug("Checking if key rotation is needed");

            var primaryKey = await GetPrimarySigningKeyAsync();

            if (primaryKey == null)
            {
                _logger.LogWarning("No primary signing key found - rotation needed");
                return true;
            }

            // 檢查金鑰是否接近到期（30天內）
            var rotationThreshold = DateTime.UtcNow.AddDays(30);
            var shouldRotate = primaryKey.ExpiresAt != null && primaryKey.ExpiresAt <= rotationThreshold;

            _logger.LogDebug("Key rotation check result: {ShouldRotate} (Key expires: {ExpiresAt})", 
                shouldRotate, primaryKey.ExpiresAt);

            return shouldRotate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key rotation need");
            return false;
        }
    }
}
