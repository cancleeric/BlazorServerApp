using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;

namespace LocalIdentityServer.Services;

/// <summary>
/// 預設金鑰管理服務實作 - 遵循 SOLID 原則
/// 整合 PersistedKeyRepository 和 KeyDataProtectionService 提供企業級金鑰管理功能
/// </summary>
public class DefaultKeyManagementService : IKeyManagementService
{
    private readonly IPersistedKeyRepository _keyRepository;
    private readonly IKeyDataProtectionService _keyProtectionService;
    private readonly ILogger<DefaultKeyManagementService> _logger;
    private readonly KeyManagementOptions _options;

    public DefaultKeyManagementService(
        IPersistedKeyRepository keyRepository,
        IKeyDataProtectionService keyProtectionService,
        ILogger<DefaultKeyManagementService> logger,
        IOptions<KeyManagementOptions> options)
    {
        _keyRepository = keyRepository ?? throw new ArgumentNullException(nameof(keyRepository));
        _keyProtectionService = keyProtectionService ?? throw new ArgumentNullException(nameof(keyProtectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SecurityKey?> GetCurrentSigningKeyAsync()
    {
        try
        {
            _logger.LogDebug("Getting current signing key");

            var primaryKey = await _keyRepository.GetPrimarySigningKeyAsync();
            if (primaryKey == null)
            {
                _logger.LogWarning("No primary signing key found");
                return null;
            }

            var securityKey = ConvertToSecurityKey(primaryKey);
            _logger.LogDebug("Retrieved current signing key: {KeyId}", primaryKey.KeyId);
            
            return securityKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current signing key");
            throw;
        }
    }

    public async Task<IEnumerable<SecurityKey>> GetValidationKeysAsync()
    {
        try
        {
            _logger.LogDebug("Getting validation keys for JWKS");

            var validKeys = await _keyRepository.GetValidSigningKeysAsync();
            var securityKeys = validKeys.Select(ConvertToSecurityKey).ToList();

            _logger.LogDebug("Retrieved {Count} validation keys", securityKeys.Count);
            return securityKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting validation keys");
            throw;
        }
    }

    public async Task<string> GenerateNewKeyAsync(int keySize = 2048, string algorithm = "RS256", TimeSpan? validityPeriod = null)
    {
        try
        {
            // 驗證參數
            if (keySize != 2048 && keySize != 4096)
            {
                throw new ArgumentException("Key size must be 2048 or 4096", nameof(keySize));
            }

            if (!IsValidAlgorithm(algorithm))
            {
                throw new ArgumentException($"Unsupported algorithm: {algorithm}", nameof(algorithm));
            }

            var validity = validityPeriod ?? TimeSpan.FromDays(_options.DefaultKeyValidityDays);
            var keyId = Guid.NewGuid().ToString("N");

            _logger.LogInformation("Generating new RSA key: KeyId={KeyId}, Size={KeySize}, Algorithm={Algorithm}", 
                keyId, keySize, algorithm);

            // 生成 RSA 金鑰
            using var rsa = RSA.Create(keySize);
            
            // 匯出私鑰和公鑰
            var privateKeyPem = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            var publicKeyPem = Convert.ToBase64String(rsa.ExportRSAPublicKey());

            // 創建金鑰實體
            // 使用 Data Protection 加密私鑰資料
            var encryptedPrivateKeyData = _keyProtectionService.ProtectKeyData(privateKeyPem);

            var keyEntity = new PersistedKeyEntity
            {
                KeyId = keyId,
                Algorithm = algorithm,
                Use = "sig",
                KeyType = "RSA",
                EncryptedKeyData = encryptedPrivateKeyData,
                PublicKeyData = publicKeyPem,
                ExpiresAt = DateTime.UtcNow.Add(validity),
                IsPrimary = false, // 新金鑰不會自動成為主要金鑰
                Version = 1,
                LastModifiedAt = DateTime.UtcNow
            };

            // 儲存到資料庫
            await _keyRepository.StoreAsync(keyEntity);

            _logger.LogInformation("New RSA key generated successfully: {KeyId}", keyId);
            return keyId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating new key");
            throw;
        }
    }

    public async Task SetPrimaryKeyAsync(string keyId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogInformation("Setting primary key: {KeyId}", keyId);

            await _keyRepository.SetPrimaryKeyAsync(keyId);

            _logger.LogInformation("Primary key set successfully: {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting primary key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task RevokeKeyAsync(string keyId, string? reason = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogInformation("Revoking key: {KeyId}, Reason: {Reason}", keyId, reason ?? "Not specified");

            await _keyRepository.RevokeKeyAsync(keyId);

            _logger.LogInformation("Key revoked successfully: {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking key: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<bool> RotateKeyIfNeededAsync()
    {
        try
        {
            _logger.LogDebug("Checking if key rotation is needed");

            var shouldRotate = await _keyRepository.ShouldRotateKeyAsync();
            if (!shouldRotate)
            {
                _logger.LogDebug("Key rotation not needed");
                return false;
            }

            _logger.LogInformation("Key rotation needed, executing rotation");

            var newKeyId = await ForceKeyRotationAsync();
            
            _logger.LogInformation("Key rotation completed. New primary key: {KeyId}", newKeyId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key rotation check");
            throw;
        }
    }

    public async Task<string> ForceKeyRotationAsync()
    {
        try
        {
            _logger.LogInformation("Forcing key rotation");

            // 生成新金鑰
            var newKeyId = await GenerateNewKeyAsync(
                _options.DefaultKeySize, 
                _options.DefaultAlgorithm, 
                TimeSpan.FromDays(_options.DefaultKeyValidityDays));

            // 設定為主要金鑰
            await SetPrimaryKeyAsync(newKeyId);

            _logger.LogInformation("Forced key rotation completed. New primary key: {KeyId}", newKeyId);
            return newKeyId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced key rotation");
            throw;
        }
    }

    public async Task<int> CleanupExpiredKeysAsync()
    {
        try
        {
            _logger.LogDebug("Starting expired keys cleanup");

            // 取得所有過期金鑰
            var expiredKeysCount = await _keyRepository.CleanupExpiredKeysAsync();

            _logger.LogInformation("Cleaned up {Count} expired keys", expiredKeysCount);
            return expiredKeysCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expired keys cleanup");
            throw;
        }
    }

    public async Task<string?> ExportPublicKeyAsJwkAsync(string keyId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogDebug("Exporting public key as JWK: {KeyId}", keyId);

            var keyEntity = await _keyRepository.GetByKeyIdAsync(keyId);
            if (keyEntity == null)
            {
                _logger.LogWarning("Key not found for JWK export: {KeyId}", keyId);
                return null;
            }

            if (string.IsNullOrEmpty(keyEntity.PublicKeyData))
            {
                _logger.LogWarning("No public key data available for: {KeyId}", keyId);
                return null;
            }

            // 轉換為 JWK 格式
            var publicKeyBytes = Convert.FromBase64String(keyEntity.PublicKeyData);
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(publicKeyBytes, out _);

            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa) { KeyId = keyId });
            var jwkJson = JsonSerializer.Serialize(jwk, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogDebug("Public key exported as JWK: {KeyId}", keyId);
            return jwkJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting public key as JWK: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<KeyStatistics> GetKeyStatisticsAsync()
    {
        try
        {
            _logger.LogDebug("Getting key statistics");

            var validKeys = await _keyRepository.GetValidSigningKeysAsync();
            var primaryKey = await _keyRepository.GetPrimarySigningKeyAsync();

            var statistics = new KeyStatistics
            {
                TotalKeys = validKeys.Count(),
                ActiveKeys = validKeys.Count(k => !k.IsRevoked && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow)),
                ExpiredKeys = validKeys.Count(k => k.ExpiresAt != null && k.ExpiresAt <= DateTime.UtcNow),
                RevokedKeys = validKeys.Count(k => k.IsRevoked),
                NextRotationTime = primaryKey?.ExpiresAt?.AddDays(-_options.RotationWarningDays),
                PrimaryKey = primaryKey != null ? new KeyInfo
                {
                    KeyId = primaryKey.KeyId,
                    Algorithm = primaryKey.Algorithm,
                    KeySize = GetKeySizeFromEncryptedKeyData(primaryKey.EncryptedKeyData),
                    CreatedAt = primaryKey.CreatedAt,
                    ExpiresAt = primaryKey.ExpiresAt,
                    IsPrimary = primaryKey.IsPrimary
                } : null
            };

            _logger.LogDebug("Key statistics retrieved: {ActiveKeys} active, {ExpiredKeys} expired, {RevokedKeys} revoked", 
                statistics.ActiveKeys, statistics.ExpiredKeys, statistics.RevokedKeys);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key statistics");
            throw;
        }
    }

    public async Task<KeyHealthCheckResult> CheckKeyHealthAsync()
    {
        try
        {
            _logger.LogDebug("Performing key health check");

            var issues = new List<string>();
            var warnings = new List<string>();

            // 檢查是否有主要金鑰
            var primaryKey = await _keyRepository.GetPrimarySigningKeyAsync();
            if (primaryKey == null)
            {
                issues.Add("No primary signing key found");
            }
            else
            {
                // 檢查主要金鑰是否接近到期
                if (primaryKey.ExpiresAt.HasValue)
                {
                    var daysUntilExpiration = (primaryKey.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
                    if (daysUntilExpiration <= _options.RotationWarningDays)
                    {
                        warnings.Add($"Primary key will expire in {daysUntilExpiration:F0} days");
                    }
                }
            }

            // 檢查是否有足夠的有效金鑰
            var validKeys = await _keyRepository.GetValidSigningKeysAsync();
            if (validKeys.Count() < _options.MinimumKeyCount)
            {
                warnings.Add($"Only {validKeys.Count()} valid keys available (minimum: {_options.MinimumKeyCount})");
            }

            var result = new KeyHealthCheckResult
            {
                IsHealthy = !issues.Any(),
                Issues = issues,
                Warnings = warnings
            };

            _logger.LogDebug("Key health check completed: {IsHealthy} (Issues: {IssueCount}, Warnings: {WarningCount})", 
                result.IsHealthy, result.Issues.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key health check");
            return new KeyHealthCheckResult
            {
                IsHealthy = false,
                Issues = new List<string> { $"Health check failed: {ex.Message}" }
            };
        }
    }

    #region Private Helper Methods

    private SecurityKey ConvertToSecurityKey(PersistedKeyEntity keyEntity)
    {
        try
        {
            if (keyEntity.KeyType == "RSA")
            {
                // 解密金鑰資料
                var decryptedKeyData = _keyProtectionService.UnprotectKeyData(keyEntity.EncryptedKeyData);
                var privateKeyBytes = Convert.FromBase64String(decryptedKeyData);
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                
                return new RsaSecurityKey(rsa.ExportParameters(true)) 
                { 
                    KeyId = keyEntity.KeyId 
                };
            }

            throw new NotSupportedException($"Key type '{keyEntity.KeyType}' is not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting persisted key to security key: {KeyId}", keyEntity.KeyId);
            throw;
        }
    }

    private static bool IsValidAlgorithm(string algorithm)
    {
        return algorithm switch
        {
            "RS256" or "RS384" or "RS512" => true,
            _ => false
        };
    }

    private int GetKeySizeFromEncryptedKeyData(string encryptedKeyData)
    {
        try
        {
            // 解密金鑰資料
            var decryptedKeyData = _keyProtectionService.UnprotectKeyData(encryptedKeyData);
            var keyBytes = Convert.FromBase64String(decryptedKeyData);
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(keyBytes, out _);
            return rsa.KeySize;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine key size from encrypted key data");
            return 0; // 無法確定金鑰大小
        }
    }

    #endregion
}

/// <summary>
/// 金鑰管理選項配置
/// </summary>
public class KeyManagementOptions
{
    public const string SectionName = "KeyManagement";

    /// <summary>
    /// 預設金鑰大小 (預設 2048)
    /// </summary>
    public int DefaultKeySize { get; set; } = 2048;

    /// <summary>
    /// 預設簽章演算法 (預設 RS256)
    /// </summary>
    public string DefaultAlgorithm { get; set; } = "RS256";

    /// <summary>
    /// 預設金鑰有效期天數 (預設 365 天)
    /// </summary>
    public int DefaultKeyValidityDays { get; set; } = 365;

    /// <summary>
    /// 金鑰輪替警告提前天數 (預設 30 天)
    /// </summary>
    public int RotationWarningDays { get; set; } = 30;

    /// <summary>
    /// 最少金鑰數量 (預設 2)
    /// </summary>
    public int MinimumKeyCount { get; set; } = 2;

    /// <summary>
    /// 是否啟用自動金鑰輪替 (預設 true)
    /// </summary>
    public bool EnableAutomaticRotation { get; set; } = true;

    /// <summary>
    /// 金鑰輪替檢查間隔小時 (預設 24 小時)
    /// </summary>
    public int RotationCheckIntervalHours { get; set; } = 24;
}