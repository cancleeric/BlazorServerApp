using LocalIdentityServer.Services.MFA;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;

namespace LocalIdentityServer.Tests.Services.MFA;

/// <summary>
/// MFA 加密服務單元測試
/// 測試敏感資料的加密與解密功能
/// 確保 MFA 密鑰的安全存儲
/// </summary>
public class MfaEncryptionServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly Mock<ILogger<MfaEncryptionService>> _mockLogger;
    private readonly MfaEncryptionService _encryptionService;

    public MfaEncryptionServiceTests()
    {
        // 建立真實的 DataProtection 服務用於測試
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddLogging();
        
        _serviceProvider = services.BuildServiceProvider();
        _dataProtectionProvider = _serviceProvider.GetRequiredService<IDataProtectionProvider>();
        
        _mockLogger = new Mock<ILogger<MfaEncryptionService>>();
        
        _encryptionService = new MfaEncryptionService(
            _dataProtectionProvider,
            _mockLogger.Object
        );
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public void EncryptSecret_WithValidInput_ShouldReturnEncryptedString()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var userId = "user123";

        // Act
        var encryptedSecret = _encryptionService.EncryptSecret(secret, userId);

        // Assert
        Assert.NotNull(encryptedSecret);
        Assert.NotEmpty(encryptedSecret);
        Assert.NotEqual(secret, encryptedSecret); // 加密後應該不同
    }

    [Fact]
    public void DecryptSecret_WithValidEncryptedData_ShouldReturnOriginalSecret()
    {
        // Arrange
        var originalSecret = "JBSWY3DPEHPK3PXP";
        var userId = "user123";
        
        var encryptedSecret = _encryptionService.EncryptSecret(originalSecret, userId);

        // Act
        var decryptedSecret = _encryptionService.DecryptSecret(encryptedSecret, userId);

        // Assert
        Assert.Equal(originalSecret, decryptedSecret);
    }

    [Fact]
    public void EncryptDecrypt_WithDifferentUserIds_ShouldFail()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var userId1 = "user123";
        var userId2 = "user456";
        
        var encryptedSecret = _encryptionService.EncryptSecret(secret, userId1);

        // Act & Assert
        Assert.Throws<CryptographicException>(() => 
            _encryptionService.DecryptSecret(encryptedSecret, userId2));
    }

    [Fact]
    public void EncryptSecret_WithSameInputs_ShouldReturnDifferentResults()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var userId = "user123";

        // Act
        var encrypted1 = _encryptionService.EncryptSecret(secret, userId);
        var encrypted2 = _encryptionService.EncryptSecret(secret, userId);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2); // 每次加密結果應該不同（因為使用不同的 IV）
        
        // 但解密結果應該相同
        var decrypted1 = _encryptionService.DecryptSecret(encrypted1, userId);
        var decrypted2 = _encryptionService.DecryptSecret(encrypted2, userId);
        Assert.Equal(secret, decrypted1);
        Assert.Equal(secret, decrypted2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EncryptSecret_WithInvalidSecret_ShouldThrowArgumentException(string? secret)
    {
        // Arrange
        var userId = "user123";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _encryptionService.EncryptSecret(secret ?? "", userId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EncryptSecret_WithInvalidUserId_ShouldThrowArgumentException(string? userId)
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _encryptionService.EncryptSecret(secret, userId ?? ""));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DecryptSecret_WithInvalidEncryptedSecret_ShouldThrowArgumentException(string? encryptedSecret)
    {
        // Arrange
        var userId = "user123";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _encryptionService.DecryptSecret(encryptedSecret ?? "", userId));
    }

    [Fact]
    public void DecryptSecret_WithCorruptedData_ShouldThrowCryptographicException()
    {
        // Arrange
        var userId = "user123";
        var corruptedData = "InvalidBase64Data!@#";

        // Act & Assert
        Assert.Throws<CryptographicException>(() => 
            _encryptionService.DecryptSecret(corruptedData, userId));
    }

    [Fact]
    public void GenerateSecureRandomString_WithDefaultLength_ShouldReturnValidString()
    {
        // Arrange
        var defaultLength = 32;

        // Act
        var randomString = _encryptionService.GenerateSecureRandomString(defaultLength);

        // Assert
        Assert.NotNull(randomString);
        Assert.Equal(defaultLength, randomString.Length);
        Assert.Matches("^[A-Za-z0-9]+$", randomString); // 只包含字母和數字
    }

    [Fact]
    public void GenerateSecureRandomString_WithCustomLength_ShouldReturnCorrectLength()
    {
        // Arrange
        var length = 16;

        // Act
        var randomString = _encryptionService.GenerateSecureRandomString(length);

        // Assert
        Assert.NotNull(randomString);
        Assert.Equal(length, randomString.Length);
    }

    [Fact]
    public void GenerateSecureRandomString_DigitsOnly_ShouldReturnOnlyDigits()
    {
        // Arrange
        var length = 8;
        var digitsOnly = true;

        // Act
        var randomString = _encryptionService.GenerateSecureRandomString(length, digitsOnly);

        // Assert
        Assert.NotNull(randomString);
        Assert.Equal(length, randomString.Length);
        Assert.Matches("^[0-9]+$", randomString); // 只包含數字
    }

    [Fact]
    public void GenerateSecureRandomString_MultipleCalls_ShouldReturnUniqueValues()
    {
        // Arrange
        var values = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            values.Add(_encryptionService.GenerateSecureRandomString(16));
        }

        // Assert
        Assert.Equal(100, values.Count); // 所有值都應該是唯一的
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateSecureRandomString_WithInvalidLength_ShouldThrowArgumentException(int length)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _encryptionService.GenerateSecureRandomString(length));
    }

    [Fact]
    public void HashData_WithSameInput_ShouldReturnSameHash()
    {
        // Arrange
        var data = "test data";

        // Act
        var hash1 = _encryptionService.HashData(data);
        var hash2 = _encryptionService.HashData(data);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashData_WithDifferentInput_ShouldReturnDifferentHash()
    {
        // Arrange
        var data1 = "test data 1";
        var data2 = "test data 2";

        // Act
        var hash1 = _encryptionService.HashData(data1);
        var hash2 = _encryptionService.HashData(data2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashData_ShouldReturnValidBase64String()
    {
        // Arrange
        var data = "test data";

        // Act
        var hash = _encryptionService.HashData(data);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        
        // 驗證是否為有效的 Base64 字串
        Assert.True(IsValidBase64(hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void HashData_WithInvalidInput_ShouldThrowArgumentException(string? data)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _encryptionService.HashData(data ?? ""));
    }

    [Fact]
    public void DataProtection_ShouldBeUserSpecific()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var user1 = "user1";
        var user2 = "user2";

        // Act
        var encrypted1 = _encryptionService.EncryptSecret(secret, user1);
        var encrypted2 = _encryptionService.EncryptSecret(secret, user2);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2); // 不同使用者的加密結果應該不同

        // 各自解密應該成功
        var decrypted1 = _encryptionService.DecryptSecret(encrypted1, user1);
        var decrypted2 = _encryptionService.DecryptSecret(encrypted2, user2);
        
        Assert.Equal(secret, decrypted1);
        Assert.Equal(secret, decrypted2);

        // 交叉解密應該失敗
        Assert.Throws<CryptographicException>(() => 
            _encryptionService.DecryptSecret(encrypted1, user2));
        Assert.Throws<CryptographicException>(() => 
            _encryptionService.DecryptSecret(encrypted2, user1));
    }

    [Fact]
    public void EncryptionService_ShouldHandleLongSecrets()
    {
        // Arrange
        var longSecret = new string('A', 1000); // 1000 字元的長密鑰
        var userId = "user123";

        // Act
        var encrypted = _encryptionService.EncryptSecret(longSecret, userId);
        var decrypted = _encryptionService.DecryptSecret(encrypted, userId);

        // Assert
        Assert.Equal(longSecret, decrypted);
    }

    [Fact]
    public void EncryptionService_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var secretWithSpecialChars = "JBSWY3DPEHPK3PXP!@#$%^&*()+=";
        var userId = "user123";

        // Act
        var encrypted = _encryptionService.EncryptSecret(secretWithSpecialChars, userId);
        var decrypted = _encryptionService.DecryptSecret(encrypted, userId);

        // Assert
        Assert.Equal(secretWithSpecialChars, decrypted);
    }

    [Fact]
    public void EncryptionService_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var unicodeSecret = "測試密鑰🔐中文字符";
        var userId = "user123";

        // Act
        var encrypted = _encryptionService.EncryptSecret(unicodeSecret, userId);
        var decrypted = _encryptionService.DecryptSecret(encrypted, userId);

        // Assert
        Assert.Equal(unicodeSecret, decrypted);
    }

    private static bool IsValidBase64(string input)
    {
        try
        {
            Convert.FromBase64String(input);
            return true;
        }
        catch
        {
            return false;
        }
    }
}