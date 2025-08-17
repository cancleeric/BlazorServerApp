using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LocalIdentityServer.Services;

namespace LocalIdentityServer.Tests.Services;

/// <summary>
/// KeyDataProtectionService 單元測試
/// 驗證金鑰資料加密與解密功能
/// </summary>
public class KeyDataProtectionServiceTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly Mock<ILogger<KeyDataProtectionService>> _mockLogger;
    private readonly KeyDataProtectionService _protectionService;

    public KeyDataProtectionServiceTests()
    {
        // 設置真實的 DataProtection 提供者
        var services = new ServiceCollection();
        services.AddDataProtection();
        var serviceProvider = services.BuildServiceProvider();
        _dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();

        _mockLogger = new Mock<ILogger<KeyDataProtectionService>>();
        _protectionService = new KeyDataProtectionService(_dataProtectionProvider, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullDataProtectionProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new KeyDataProtectionService(null!, _mockLogger.Object));

        Assert.Equal("dataProtectionProvider", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new KeyDataProtectionService(_dataProtectionProvider, null!));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void ProtectKeyData_WithValidKeyData_ReturnsEncryptedData()
    {
        // Arrange
        var keyData = "test-private-key-data";

        // Act
        var encryptedData = _protectionService.ProtectKeyData(keyData);

        // Assert
        Assert.NotNull(encryptedData);
        Assert.NotEmpty(encryptedData);
        Assert.NotEqual(keyData, encryptedData);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProtectKeyData_WithInvalidKeyData_ThrowsArgumentException(string invalidKeyData)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _protectionService.ProtectKeyData(invalidKeyData));

        Assert.Contains("Key data cannot be null or empty", exception.Message);
        Assert.Equal("keyData", exception.ParamName);
    }

    [Fact]
    public void UnprotectKeyData_WithValidEncryptedData_ReturnsOriginalData()
    {
        // Arrange
        var originalKeyData = "test-private-key-data-12345";
        var encryptedData = _protectionService.ProtectKeyData(originalKeyData);

        // Act
        var decryptedData = _protectionService.UnprotectKeyData(encryptedData);

        // Assert
        Assert.Equal(originalKeyData, decryptedData);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnprotectKeyData_WithInvalidEncryptedData_ThrowsArgumentException(string invalidEncryptedData)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _protectionService.UnprotectKeyData(invalidEncryptedData));

        Assert.Contains("Encrypted key data cannot be null or empty", exception.Message);
        Assert.Equal("encryptedKeyData", exception.ParamName);
    }

    [Fact]
    public void UnprotectKeyData_WithCorruptedEncryptedData_ThrowsInvalidOperationException()
    {
        // Arrange
        var corruptedData = "corrupted-encrypted-data";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _protectionService.UnprotectKeyData(corruptedData));

        Assert.Contains("Failed to unprotect key data", exception.Message);
    }

    [Fact]
    public void IsValidEncryptedData_WithValidEncryptedData_ReturnsTrue()
    {
        // Arrange
        var keyData = "test-private-key-data";
        var encryptedData = _protectionService.ProtectKeyData(keyData);

        // Act
        var isValid = _protectionService.IsValidEncryptedData(encryptedData);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidEncryptedData_WithCorruptedData_ReturnsFalse()
    {
        // Arrange
        var corruptedData = "corrupted-encrypted-data";

        // Act
        var isValid = _protectionService.IsValidEncryptedData(corruptedData);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidEncryptedData_WithInvalidData_ReturnsFalse(string invalidData)
    {
        // Act
        var isValid = _protectionService.IsValidEncryptedData(invalidData);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ProtectAndUnprotect_WithLongKeyData_WorksCorrectly()
    {
        // Arrange
        var longKeyData = new string('A', 4096); // 4KB 金鑰資料

        // Act
        var encryptedData = _protectionService.ProtectKeyData(longKeyData);
        var decryptedData = _protectionService.UnprotectKeyData(encryptedData);

        // Assert
        Assert.Equal(longKeyData, decryptedData);
    }

    [Fact]
    public void ProtectKeyData_MultipleCalls_ProducesDifferentEncryptedData()
    {
        // Arrange
        var keyData = "test-private-key-data";

        // Act
        var encryptedData1 = _protectionService.ProtectKeyData(keyData);
        var encryptedData2 = _protectionService.ProtectKeyData(keyData);

        // Assert
        Assert.NotEqual(encryptedData1, encryptedData2); // 每次加密應該產生不同的結果
        
        // 但解密後應該都是原始資料
        Assert.Equal(keyData, _protectionService.UnprotectKeyData(encryptedData1));
        Assert.Equal(keyData, _protectionService.UnprotectKeyData(encryptedData2));
    }

    [Fact]
    public void ProtectKeyData_WithRealRSAKeyData_WorksCorrectly()
    {
        // Arrange
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        var privateKeyBase64 = Convert.ToBase64String(privateKeyBytes);

        // Act
        var encryptedData = _protectionService.ProtectKeyData(privateKeyBase64);
        var decryptedData = _protectionService.UnprotectKeyData(encryptedData);

        // Assert
        Assert.Equal(privateKeyBase64, decryptedData);
        
        // 驗證解密後的資料可以正確導入 RSA
        var decryptedKeyBytes = Convert.FromBase64String(decryptedData);
        using var rsaFromDecrypted = System.Security.Cryptography.RSA.Create();
        rsaFromDecrypted.ImportRSAPrivateKey(decryptedKeyBytes, out _);
        Assert.Equal(2048, rsaFromDecrypted.KeySize);
    }
}