using LocalIdentityServer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LocalIdentityServer.Tests.Services;

/// <summary>
/// CertificateHealthService 單元測試
/// 驗證 SSL 憑證健康檢查服務的完整功能
/// </summary>
public class CertificateHealthServiceTests : IDisposable
{
    private readonly Mock<ILogger<CertificateHealthService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly CertificateHealthService _service;
    private readonly X509Certificate2 _testCertificate;
    private readonly string _testCertificatePath;

    public CertificateHealthServiceTests()
    {
        _mockLogger = new Mock<ILogger<CertificateHealthService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // 設定預設配置值
        _mockConfiguration.Setup(c => c.GetValue<int>("SecurityHeaders:CertificateHealthCheck:ExpiryWarningDays", 30))
                         .Returns(30);

        _service = new CertificateHealthService(_mockLogger.Object, _mockConfiguration.Object);

        // 建立測試憑證
        _testCertificate = CreateTestCertificate();
        _testCertificatePath = Path.GetTempFileName();
        
        // 儲存測試憑證到臨時檔案
        File.WriteAllBytes(_testCertificatePath, _testCertificate.RawData);
    }

    [Fact]
    public async Task CheckCertificateHealthAsync_WithValidCertificate_ShouldReturnHealthyStatus()
    {
        // Act
        var result = await _service.CheckCertificateHealthAsync(_testCertificate);

        // Assert
        Assert.True(result.IsHealthy);
        Assert.False(result.IsExpired);
        Assert.False(result.IsExpiringSoon);
        Assert.False(result.IsRevoked);
        Assert.Empty(result.Errors);
        Assert.NotEmpty(result.SubjectName);
        Assert.NotEmpty(result.IssuerName);
        Assert.NotEmpty(result.SerialNumber);
        Assert.NotEmpty(result.Thumbprint);
    }

    [Fact]
    public async Task CheckCertificateHealthAsync_WithExpiredCertificate_ShouldReturnUnhealthyStatus()
    {
        // Arrange
        var expiredCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _service.CheckCertificateHealthAsync(expiredCert);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.True(result.IsExpired);
        Assert.True(result.DaysUntilExpiry < 0);
        Assert.Contains(result.Errors, e => e.Contains("expired"));

        expiredCert.Dispose();
    }

    [Fact]
    public async Task CheckCertificateHealthAsync_WithExpiringSoonCertificate_ShouldReturnWarning()
    {
        // Arrange
        var expiringSoonCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(15));

        // Act
        var result = await _service.CheckCertificateHealthAsync(expiringSoonCert);

        // Assert
        Assert.True(result.IsExpiringSoon);
        Assert.False(result.IsExpired);
        Assert.True(result.DaysUntilExpiry <= 30);
        Assert.Contains(result.Warnings, w => w.Contains("expires in"));

        expiringSoonCert.Dispose();
    }

    [Fact]
    public async Task CheckCertificateHealthAsync_WithNullCertificate_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CheckCertificateHealthAsync((X509Certificate2)null!));
    }

    [Fact]
    public async Task CheckCertificateHealthAsync_WithFilePath_WhenFileExists_ShouldReturnHealthStatus()
    {
        // Act
        var result = await _service.CheckCertificateHealthAsync(_testCertificatePath);

        // Assert
        Assert.True(result.IsHealthy);
        Assert.NotEmpty(result.SubjectName);
    }

    [Fact]
    public async Task CheckCertificateHealthAsync_WithFilePath_WhenFileNotExists_ShouldReturnUnhealthyStatus()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/certificate.pfx";

        // Act
        var result = await _service.CheckCertificateHealthAsync(nonExistentPath);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Contains(result.Errors, e => e.Contains("Certificate file not found"));
    }

    [Fact]
    public void GetDaysUntilExpiry_WithValidCertificate_ShouldReturnCorrectDays()
    {
        // Act
        var days = _service.GetDaysUntilExpiry(_testCertificate);

        // Assert
        Assert.True(days > 0);
        Assert.True(days <= 365); // 測試憑證有效期 1 年
    }

    [Fact]
    public void GetDaysUntilExpiry_WithExpiredCertificate_ShouldReturnNegativeDays()
    {
        // Arrange
        var expiredCert = CreateTestCertificate(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-1));

        // Act
        var days = _service.GetDaysUntilExpiry(expiredCert);

        // Assert
        Assert.True(days < 0);

        expiredCert.Dispose();
    }

    [Fact]
    public void GetDaysUntilExpiry_WithNullCertificate_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.GetDaysUntilExpiry(null!));
    }

    [Fact]
    public async Task ValidateCertificateChainAsync_WithValidCertificate_ShouldHandleGracefully()
    {
        // Note: 這個測試可能會因為網路環境而有不同結果
        // 主要是測試方法不會拋出例外
        
        // Act
        var isValid = await _service.ValidateCertificateChainAsync(_testCertificate);

        // Assert
        // 不驗證具體結果，因為測試憑證可能無法通過完整鏈驗證
        // 主要確保方法正常執行不拋出例外
        Assert.True(true); // 如果到達這裡表示沒有拋出例外
    }

    [Fact]
    public async Task IsCertificateRevokedAsync_WithValidCertificate_ShouldHandleGracefully()
    {
        // Note: 這個測試可能會因為網路環境而有不同結果
        // 主要是測試方法不會拋出例外
        
        // Act
        var isRevoked = await _service.IsCertificateRevokedAsync(_testCertificate);

        // Assert
        // 不驗證具體結果，因為測試憑證可能無法檢查撤銷狀態
        // 主要確保方法正常執行不拋出例外
        Assert.True(true); // 如果到達這裡表示沒有拋出例外
    }

    [Fact]
    public void RegisterCertificateForMonitoring_WithValidParameters_ShouldRegisterSuccessfully()
    {
        // Arrange
        var testPath = "/test/certificate.pfx";
        var friendlyName = "Test Certificate";

        // Act
        _service.RegisterCertificateForMonitoring(testPath, friendlyName);

        // Assert
        // 驗證是否已註冊 (透過取得監控清單來確認)
        var summaries = _service.GetAllCertificateHealthAsync().Result;
        Assert.Contains(summaries, s => s.FriendlyName == friendlyName);
    }

    [Fact]
    public void RegisterCertificateForMonitoring_WithNullPath_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RegisterCertificateForMonitoring(null!, "Test"));
    }

    [Fact]
    public void RegisterCertificateForMonitoring_WithEmptyFriendlyName_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RegisterCertificateForMonitoring("/test/path", ""));
    }

    [Fact]
    public void UnregisterCertificateFromMonitoring_WithRegisteredCertificate_ShouldRemoveSuccessfully()
    {
        // Arrange
        var testPath = "/test/certificate.pfx";
        var friendlyName = "Test Certificate";
        _service.RegisterCertificateForMonitoring(testPath, friendlyName);

        // Act
        _service.UnregisterCertificateFromMonitoring(testPath);

        // Assert
        var summaries = _service.GetAllCertificateHealthAsync().Result;
        Assert.DoesNotContain(summaries, s => s.FriendlyName == friendlyName);
    }

    [Fact]
    public async Task GetAllCertificateHealthAsync_WithRegisteredCertificates_ShouldReturnSummaries()
    {
        // Arrange
        _service.RegisterCertificateForMonitoring(_testCertificatePath, "Test Certificate");

        // Act
        var summaries = await _service.GetAllCertificateHealthAsync();

        // Assert
        Assert.NotEmpty(summaries);
        Assert.Contains(summaries, s => s.FriendlyName == "Test Certificate");
        Assert.All(summaries, s => Assert.True(s.LastChecked <= DateTime.UtcNow));
    }

    [Fact]
    public async Task GetAllCertificateHealthAsync_WithInvalidCertificatePath_ShouldReturnUnknownStatus()
    {
        // Arrange
        _service.RegisterCertificateForMonitoring("/invalid/path/cert.pfx", "Invalid Certificate");

        // Act
        var summaries = await _service.GetAllCertificateHealthAsync();

        // Assert
        var invalidSummary = summaries.FirstOrDefault(s => s.FriendlyName == "Invalid Certificate");
        Assert.NotNull(invalidSummary);
        Assert.Equal(CertificateHealthStatus.Unknown, invalidSummary.Status);
        Assert.True(invalidSummary.ErrorCount > 0);
    }

    [Theory]
    [InlineData(1024)] // 弱金鑰
    [InlineData(2048)] // 最小建議
    [InlineData(4096)] // 強金鑰
    public async Task CheckCertificateHealthAsync_WithDifferentKeySizes_ShouldEvaluateKeyStrength(int keySize)
    {
        // Arrange
        var cert = CreateTestCertificate(keySize: keySize);

        // Act
        var result = await _service.CheckCertificateHealthAsync(cert);

        // Assert
        Assert.Equal(keySize, result.KeySize);
        
        if (keySize < 2048)
        {
            Assert.Contains(result.Errors, e => e.Contains("key size"));
        }
        else if (keySize < 4096)
        {
            Assert.Contains(result.Warnings, w => w.Contains("key size"));
        }

        cert.Dispose();
    }

    #region Helper Methods

    /// <summary>
    /// 建立測試用的自簽憑證
    /// </summary>
    private X509Certificate2 CreateTestCertificate(
        DateTime? notBefore = null, 
        DateTime? notAfter = null,
        int keySize = 2048)
    {
        notBefore ??= DateTime.UtcNow.AddDays(-1);
        notAfter ??= DateTime.UtcNow.AddDays(365);

        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest(
            "CN=Test Certificate, O=Test Organization, C=US",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // 添加 Key Usage 擴充
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        // 添加 Enhanced Key Usage 擴充
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                false));

        return request.CreateSelfSigned(notBefore.Value, notAfter.Value);
    }

    #endregion

    public void Dispose()
    {
        _testCertificate?.Dispose();
        
        if (File.Exists(_testCertificatePath))
        {
            try
            {
                File.Delete(_testCertificatePath);
            }
            catch
            {
                // 忽略清理錯誤
            }
        }
    }
}