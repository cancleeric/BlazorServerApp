using LocalIdentityServer.Services.MFA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalIdentityServer.Tests.Services.MFA;

/// <summary>
/// TOTP 服務單元測試
/// 遵循 AAA (Arrange, Act, Assert) 模式
/// 測試 RFC 6238 TOTP 標準實作的正確性
/// </summary>
public class TotpServiceTests
{
    private readonly Mock<ILogger<TotpService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly TotpService _totpService;

    public TotpServiceTests()
    {
        _mockLogger = new Mock<ILogger<TotpService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // 設定預設配置
        _mockConfiguration.Setup(c => c["Mfa:Totp:TimeStepSeconds"]).Returns("30");
        _mockConfiguration.Setup(c => c["Mfa:Totp:Digits"]).Returns("6");
        _mockConfiguration.Setup(c => c["Mfa:Totp:HashMode"]).Returns("Sha1");

        _totpService = new TotpService(_mockConfiguration.Object, _mockLogger.Object);
    }

    [Fact]
    public void GenerateSecret_ShouldReturnValidBase32String()
    {
        // Arrange & Act
        var secret = _totpService.GenerateSecret();

        // Assert
        Assert.NotNull(secret);
        Assert.NotEmpty(secret);
        Assert.Equal(32, secret.Length); // Base32 編碼的 32 字元
        Assert.Matches("^[A-Z2-7]+$", secret); // Base32 字元集
    }

    [Fact]
    public void GenerateSecret_ShouldReturnUniqueSecrets()
    {
        // Arrange
        var secrets = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            secrets.Add(_totpService.GenerateSecret());
        }

        // Assert
        Assert.Equal(100, secrets.Count); // 所有密鑰都應該是唯一的
    }

    [Fact]
    public void GenerateTotpUri_ShouldReturnValidOtpAuthUri()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var accountName = "test@example.com";

        // Act
        var uri = _totpService.GenerateTotpUri(accountName, secret);

        // Assert
        Assert.NotNull(uri);
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=LocalIdentityServer", uri);
    }

    [Fact]
    public void VerifyTotp_WithValidCode_ShouldReturnTrue()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        
        // 生成當前時間的 TOTP 代碼
        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        // Act
        var result = _totpService.VerifyTotp(secret, code);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyTotp_WithInvalidCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var invalidCode = "000000";

        // Act
        var result = _totpService.VerifyTotp(secret, invalidCode);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyTotp_WithEmptySecret_ShouldReturnFalse()
    {
        // Arrange
        var emptySecret = "";
        var code = "123456";

        // Act
        var result = _totpService.VerifyTotp(emptySecret, code);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyTotp_WithEmptyCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var emptyCode = "";

        // Act
        var result = _totpService.VerifyTotp(secret, emptyCode);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyTotpWithTolerance_WithValidCodeInTimeWindow_ShouldReturnValid()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        // Act
        var result = _totpService.VerifyTotpWithTolerance(secret, code, 1);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.TimeStep >= -1 && result.TimeStep <= 1);
    }

    [Fact]
    public void VerifyTotpWithTolerance_WithCodeFromPreviousTimeStep_ShouldReturnValid()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret));
        
        // 計算前一個時間步的代碼
        var previousTime = DateTimeOffset.UtcNow.AddSeconds(-30);
        var previousCode = totp.ComputeTotp(previousTime.DateTime);

        // Act
        var result = _totpService.VerifyTotpWithTolerance(secret, previousCode, 1);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(-1, result.TimeStep);
    }

    [Fact]
    public void VerifyTotpWithTolerance_WithCodeFromNextTimeStep_ShouldReturnValid()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret));
        
        // 計算下一個時間步的代碼
        var nextTime = DateTimeOffset.UtcNow.AddSeconds(30);
        var nextCode = totp.ComputeTotp(nextTime.DateTime);

        // Act
        var result = _totpService.VerifyTotpWithTolerance(secret, nextCode, 1);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1, result.TimeStep);
    }

    [Fact]
    public void VerifyTotpWithTolerance_WithCodeOutsideTolerance_ShouldReturnInvalid()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";
        var invalidCode = "000000";

        // Act
        var result = _totpService.VerifyTotpWithTolerance(secret, invalidCode, 1);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyTotpWithTolerance_WithInvalidSecret_ShouldReturnInvalid()
    {
        // Arrange
        var invalidSecret = "INVALID";
        var code = "123456";

        // Act
        var result = _totpService.VerifyTotpWithTolerance(invalidSecret, code, 1);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")] // 太短
    [InlineData("1234567")] // 太長
    [InlineData("abcdef")] // 非數字
    public void VerifyTotp_WithInvalidCodeFormats_ShouldReturnFalse(string code)
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";

        // Act
        var result = _totpService.VerifyTotp(secret, code);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("INVALID")]
    [InlineData("123456789")] // 非 Base32
    public void VerifyTotp_WithInvalidSecrets_ShouldReturnFalse(string secret)
    {
        // Arrange
        var code = "123456";

        // Act
        var result = _totpService.VerifyTotp(secret, code);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsValidSecret_WithValidSecret_ShouldReturnTrue()
    {
        // Arrange
        var validSecret = "JBSWY3DPEHPK3PXP";

        // Act
        var result = _totpService.IsValidSecret(validSecret);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidSecret_WithInvalidSecret_ShouldReturnFalse()
    {
        // Arrange
        var invalidSecret = "InvalidSecret123";

        // Act
        var result = _totpService.IsValidSecret(invalidSecret);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCurrentTotp_WithValidSecret_ShouldReturnSixDigitCode()
    {
        // Arrange
        var secret = "JBSWY3DPEHPK3PXP";

        // Act
        var currentCode = _totpService.GetCurrentTotp(secret);

        // Assert
        Assert.NotNull(currentCode);
        Assert.Equal(6, currentCode.Length);
        Assert.Matches("^[0-9]+$", currentCode);
    }

    [Fact]
    public void GetRemainingTime_ShouldReturnValidTime()
    {
        // Act
        var remainingTime = _totpService.GetRemainingTime();

        // Assert
        Assert.True(remainingTime >= 0 && remainingTime <= 30);
    }
}