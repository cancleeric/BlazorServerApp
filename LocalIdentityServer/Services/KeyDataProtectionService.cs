using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace LocalIdentityServer.Services;

/// <summary>
/// 金鑰資料保護服務實作 - 遵循單一責任原則 (SRP)
/// 使用 ASP.NET Core Data Protection API 進行金鑰加密
/// </summary>
public class KeyDataProtectionService : IKeyDataProtectionService
{
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<KeyDataProtectionService> _logger;

    private const string ProtectorPurpose = "LocalIdentityServer.KeyData.Protection.v1";

    public KeyDataProtectionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<KeyDataProtectionService> logger)
    {
        if (dataProtectionProvider == null)
            throw new ArgumentNullException(nameof(dataProtectionProvider));

        _dataProtector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("KeyDataProtectionService initialized with purpose: {Purpose}", ProtectorPurpose);
    }

    public string ProtectKeyData(string keyData)
    {
        if (string.IsNullOrWhiteSpace(keyData))
            throw new ArgumentException("Key data cannot be null or empty", nameof(keyData));

        try
        {
            _logger.LogDebug("Protecting key data (length: {Length})", keyData.Length);
            
            var protectedData = _dataProtector.Protect(keyData);
            
            _logger.LogDebug("Key data protected successfully");
            return protectedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect key data");
            throw new InvalidOperationException("Failed to protect key data", ex);
        }
    }

    public string UnprotectKeyData(string encryptedKeyData)
    {
        if (string.IsNullOrWhiteSpace(encryptedKeyData))
            throw new ArgumentException("Encrypted key data cannot be null or empty", nameof(encryptedKeyData));

        try
        {
            _logger.LogDebug("Unprotecting key data (length: {Length})", encryptedKeyData.Length);
            
            var unprotectedData = _dataProtector.Unprotect(encryptedKeyData);
            
            _logger.LogDebug("Key data unprotected successfully");
            return unprotectedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect key data");
            throw new InvalidOperationException("Failed to unprotect key data", ex);
        }
    }

    public bool IsValidEncryptedData(string encryptedKeyData)
    {
        if (string.IsNullOrWhiteSpace(encryptedKeyData))
            return false;

        try
        {
            _logger.LogDebug("Validating encrypted key data");
            
            // 嘗試解密來驗證資料有效性
            _dataProtector.Unprotect(encryptedKeyData);
            
            _logger.LogDebug("Encrypted key data is valid");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Encrypted key data validation failed");
            return false;
        }
    }
}