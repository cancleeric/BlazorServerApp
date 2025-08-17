using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 加密服務實作 - 使用 ASP.NET Core Data Protection
/// 遵循單一責任原則 (SRP) - 專責處理 MFA 相關加密
/// 遵循依賴反轉原則 (DIP) - 透過 Data Protection API 抽象加密實作
/// </summary>
public class MfaEncryptionService : IMfaEncryptionService
{
    private readonly IDataProtector _secretProtector;
    private readonly IDataProtector _generalProtector;
    private readonly ILogger<MfaEncryptionService> _logger;
    private const string SecretPurpose = "MFA.TOTP.Secret";
    private const string GeneralPurpose = "MFA.General";

    public MfaEncryptionService(IDataProtectionProvider dataProtectionProvider, ILogger<MfaEncryptionService> logger)
    {
        _logger = logger;
        
        // 為不同用途建立專用的 Data Protector
        _secretProtector = dataProtectionProvider.CreateProtector(SecretPurpose);
        _generalProtector = dataProtectionProvider.CreateProtector(GeneralPurpose);
        
        _logger.LogInformation("MFA encryption service initialized with Data Protection");
    }

    public string EncryptSecret(string secret, string userId)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));
        
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        try
        {
            // 使用使用者ID作為額外的上下文，提供更強的隔離
            var protectorWithUserId = _secretProtector.CreateProtector(userId);
            var encryptedSecret = protectorWithUserId.Protect(secret);
            
            _logger.LogDebug("TOTP secret encrypted successfully for user: {UserId}", userId);
            
            return encryptedSecret;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt TOTP secret for user: {UserId}", userId);
            throw new InvalidOperationException("Failed to encrypt TOTP secret", ex);
        }
    }

    public string DecryptSecret(string encryptedSecret, string userId)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
            throw new ArgumentException("Encrypted secret cannot be null or empty", nameof(encryptedSecret));
        
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        try
        {
            var protectorWithUserId = _secretProtector.CreateProtector(userId);
            var decryptedSecret = protectorWithUserId.Unprotect(encryptedSecret);
            
            _logger.LogDebug("TOTP secret decrypted successfully for user: {UserId}", userId);
            
            return decryptedSecret;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt TOTP secret for user: {UserId}", userId);
            throw new InvalidOperationException("Failed to decrypt TOTP secret", ex);
        }
    }

    public string EncryptData(string data, string purpose = "general")
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Data cannot be null or empty", nameof(data));

        try
        {
            var protector = purpose == "general" 
                ? _generalProtector 
                : _generalProtector.CreateProtector(purpose);
            
            var encryptedData = protector.Protect(data);
            
            _logger.LogDebug("Data encrypted successfully with purpose: {Purpose}", purpose);
            
            return encryptedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data with purpose: {Purpose}", purpose);
            throw new InvalidOperationException($"Failed to encrypt data for purpose: {purpose}", ex);
        }
    }

    public string DecryptData(string encryptedData, string purpose = "general")
    {
        if (string.IsNullOrWhiteSpace(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        try
        {
            var protector = purpose == "general" 
                ? _generalProtector 
                : _generalProtector.CreateProtector(purpose);
            
            var decryptedData = protector.Unprotect(encryptedData);
            
            _logger.LogDebug("Data decrypted successfully with purpose: {Purpose}", purpose);
            
            return decryptedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data with purpose: {Purpose}", purpose);
            throw new InvalidOperationException($"Failed to decrypt data for purpose: {purpose}", ex);
        }
    }

    public string GenerateSecureRandomString(int length, bool includeSpecialChars = false)
    {
        if (length <= 0)
            throw new ArgumentException("Length must be positive", nameof(length));

        try
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
            
            var characterSet = includeSpecialChars ? chars + specialChars : chars;
            
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            
            var result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(characterSet[bytes[i] % characterSet.Length]);
            }
            
            _logger.LogDebug("Generated secure random string of length: {Length}", length);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate secure random string");
            throw new InvalidOperationException("Failed to generate secure random string", ex);
        }
    }

    public string HashData(string data, string? salt = null)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Data cannot be null or empty", nameof(data));

        try
        {
            // 如果沒有提供鹽值，產生一個新的
            if (string.IsNullOrEmpty(salt))
            {
                salt = GenerateSecureRandomString(16);
            }

            using var sha256 = SHA256.Create();
            var saltedData = data + salt;
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedData));
            var hashString = Convert.ToBase64String(hashBytes);
            
            // 將鹽值和雜湊值組合 (格式: salt:hash)
            var result = $"{salt}:{hashString}";
            
            _logger.LogDebug("Data hashed successfully");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash data");
            throw new InvalidOperationException("Failed to hash data", ex);
        }
    }

    public bool VerifyHash(string data, string hash, string? salt = null)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash cannot be null or empty", nameof(hash));

        try
        {
            string actualSalt;
            string actualHash;

            // 如果雜湊值包含鹽值 (格式: salt:hash)
            if (hash.Contains(':') && salt == null)
            {
                var parts = hash.Split(':', 2);
                if (parts.Length != 2)
                    return false;
                
                actualSalt = parts[0];
                actualHash = parts[1];
            }
            else
            {
                actualSalt = salt ?? "";
                actualHash = hash;
            }

            // 計算預期的雜湊值
            using var sha256 = SHA256.Create();
            var saltedData = data + actualSalt;
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedData));
            var expectedHash = Convert.ToBase64String(hashBytes);

            var isValid = string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
            
            _logger.LogDebug("Hash verification result: {IsValid}", isValid);
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify hash");
            return false;
        }
    }

    public string ReencryptData(string oldEncryptedData, string purpose = "general")
    {
        if (string.IsNullOrWhiteSpace(oldEncryptedData))
            throw new ArgumentException("Old encrypted data cannot be null or empty", nameof(oldEncryptedData));

        try
        {
            // 先解密舊資料
            var decryptedData = DecryptData(oldEncryptedData, purpose);
            
            // 重新加密
            var reencryptedData = EncryptData(decryptedData, purpose);
            
            _logger.LogDebug("Data re-encrypted successfully with purpose: {Purpose}", purpose);
            
            return reencryptedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-encrypt data with purpose: {Purpose}", purpose);
            throw new InvalidOperationException($"Failed to re-encrypt data for purpose: {purpose}", ex);
        }
    }

    public bool ValidateIntegrity(string encryptedData, string purpose = "general")
    {
        if (string.IsNullOrWhiteSpace(encryptedData))
            return false;

        try
        {
            // 嘗試解密來驗證完整性
            DecryptData(encryptedData, purpose);
            
            _logger.LogDebug("Data integrity validation passed for purpose: {Purpose}", purpose);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data integrity validation failed for purpose: {Purpose}", purpose);
            return false;
        }
    }
}