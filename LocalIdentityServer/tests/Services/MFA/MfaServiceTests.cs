using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Services.MFA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalIdentityServer.Tests.Services.MFA;

/// <summary>
/// MFA 主要服務單元測試
/// 測試 MFA 服務的核心業務邏輯
/// 遵循 AAA (Arrange, Act, Assert) 模式
/// </summary>
public class MfaServiceTests
{
    private readonly Mock<IMfaRepository> _mockMfaRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ITotpService> _mockTotpService;
    private readonly Mock<ISmsService> _mockSmsService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IMfaBackupCodeService> _mockBackupCodeService;
    private readonly Mock<IMfaEncryptionService> _mockEncryptionService;
    private readonly Mock<IMfaAuditService> _mockAuditService;
    private readonly Mock<ILogger<MfaService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly MfaService _mfaService;

    public MfaServiceTests()
    {
        _mockMfaRepository = new Mock<IMfaRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockTotpService = new Mock<ITotpService>();
        _mockSmsService = new Mock<ISmsService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockBackupCodeService = new Mock<IMfaBackupCodeService>();
        _mockEncryptionService = new Mock<IMfaEncryptionService>();
        _mockAuditService = new Mock<IMfaAuditService>();
        _mockLogger = new Mock<ILogger<MfaService>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // 設定預設配置
        _mockConfiguration.Setup(c => c["Mfa:MaxFailedAttempts"]).Returns("5");
        _mockConfiguration.Setup(c => c["Mfa:LockoutDurationMinutes"]).Returns("15");
        _mockConfiguration.Setup(c => c["Mfa:RequireForNewUsers"]).Returns("false");

        _mfaService = new MfaService(
            _mockMfaRepository.Object,
            _mockUserRepository.Object,
            _mockTotpService.Object,
            _mockSmsService.Object,
            _mockEmailService.Object,
            _mockBackupCodeService.Object,
            _mockEncryptionService.Object,
            _mockAuditService.Object,
            _mockLogger.Object,
            _mockConfiguration.Object
        );
    }

    [Fact]
    public async Task IsMfaEnabledAsync_WithMfaEnabledUser_ShouldReturnTrue()
    {
        // Arrange
        var userId = "user123";
        var user = new UserEntity
        {
            Id = userId,
            IsMfaEnabled = true
        };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _mfaService.IsMfaEnabledAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsMfaEnabledAsync_WithMfaDisabledUser_ShouldReturnFalse()
    {
        // Arrange
        var userId = "user123";
        var user = new UserEntity
        {
            Id = userId,
            IsMfaEnabled = false
        };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _mfaService.IsMfaEnabledAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsMfaEnabledAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var userId = "nonexistent";

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _mfaService.IsMfaEnabledAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnableMfaAsync_WithTotpMethod_ShouldSucceed()
    {
        // Arrange
        var userId = "user123";
        var method = "TOTP";
        var deviceName = "iPhone";
        var secret = "JBSWY3DPEHPK3PXP";
        var encryptedSecret = "encrypted_secret";

        var user = new UserEntity
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _mockMfaRepository.Setup(x => x.GetByUserIdAndMethodAsync(userId, method))
            .ReturnsAsync((UserMfaEntity?)null);

        _mockMfaRepository.Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(new List<UserMfaEntity>());

        _mockTotpService.Setup(x => x.GenerateSecret())
            .Returns(secret);

        _mockEncryptionService.Setup(x => x.EncryptSecret(secret, userId))
            .Returns(encryptedSecret);

        _mockMfaRepository.Setup(x => x.CreateAsync(It.IsAny<UserMfaEntity>()))
            .ReturnsAsync((UserMfaEntity entity) => entity);

        _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<UserEntity>()))
            .Returns(Task.CompletedTask);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mfaService.EnableMfaAsync(userId, method, deviceName);

        // Assert
        Assert.True(result);
        _mockMfaRepository.Verify(x => x.CreateAsync(It.Is<UserMfaEntity>(m => 
            m.UserId == userId && 
            m.Method == method && 
            m.DeviceName == deviceName &&
            m.IsEnabled &&
            m.IsPrimary && // 第一個方法應該是主要方法
            m.EncryptedSecret == encryptedSecret
        )), Times.Once);

        _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<UserEntity>(u => 
            u.IsMfaEnabled && 
            u.DefaultMfaMethod == method
        )), Times.Once);
    }

    [Fact]
    public async Task EnableMfaAsync_WithInvalidUserId_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = "";
        var method = "TOTP";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _mfaService.EnableMfaAsync(userId, method));
    }

    [Fact]
    public async Task EnableMfaAsync_WithInvalidMethod_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = "user123";
        var method = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _mfaService.EnableMfaAsync(userId, method));
    }

    [Fact]
    public async Task EnableMfaAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var userId = "nonexistent";
        var method = "TOTP";

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _mfaService.EnableMfaAsync(userId, method);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnableMfaAsync_WithAlreadyEnabledMethod_ShouldReturnTrue()
    {
        // Arrange
        var userId = "user123";
        var method = "TOTP";

        var user = new UserEntity { Id = userId };
        var existingMethod = new UserMfaEntity
        {
            UserId = userId,
            Method = method,
            IsEnabled = true
        };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _mockMfaRepository.Setup(x => x.GetByUserIdAndMethodAsync(userId, method))
            .ReturnsAsync(existingMethod);

        // Act
        var result = await _mfaService.EnableMfaAsync(userId, method);

        // Assert
        Assert.True(result);
        _mockMfaRepository.Verify(x => x.CreateAsync(It.IsAny<UserMfaEntity>()), Times.Never);
    }

    [Fact]
    public async Task DisableMfaAsync_WithExistingMethod_ShouldSucceed()
    {
        // Arrange
        var userId = "user123";
        var method = "TOTP";

        var mfaMethod = new UserMfaEntity
        {
            Id = "mfa123",
            UserId = userId,
            Method = method,
            IsEnabled = true
        };

        var user = new UserEntity
        {
            Id = userId,
            IsMfaEnabled = true
        };

        _mockMfaRepository.Setup(x => x.GetByUserIdAndMethodAsync(userId, method))
            .ReturnsAsync(mfaMethod);

        _mockMfaRepository.Setup(x => x.GetEnabledMfaMethodsAsync(userId))
            .ReturnsAsync(new List<UserMfaEntity>()); // 沒有其他啟用的方法

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _mockMfaRepository.Setup(x => x.UpdateAsync(It.IsAny<UserMfaEntity>()))
            .ReturnsAsync((UserMfaEntity entity) => entity);

        _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<UserEntity>()))
            .Returns(Task.CompletedTask);

        _mockBackupCodeService.Setup(x => x.RevokeAllBackupCodesAsync(userId, userId, It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mfaService.DisableMfaAsync(userId, method);

        // Assert
        Assert.True(result);
        _mockMfaRepository.Verify(x => x.UpdateAsync(It.Is<UserMfaEntity>(m => 
            !m.IsEnabled
        )), Times.Once);

        _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<UserEntity>(u => 
            !u.IsMfaEnabled && 
            u.DefaultMfaMethod == null
        )), Times.Once);
    }

    [Fact]
    public async Task VerifyMfaCodeAsync_WithValidTotpCode_ShouldReturnValid()
    {
        // Arrange
        var userId = "user123";
        var method = "TOTP";
        var code = "123456";
        var context = new MfaVerificationContext
        {
            IpAddress = "192.168.1.1",
            UserAgent = "TestAgent"
        };

        var mfaMethod = new UserMfaEntity
        {
            UserId = userId,
            Method = method,
            EncryptedSecret = "encrypted_secret",
            FailedAttempts = 0,
            LockedUntil = null
        };

        var decryptedSecret = "JBSWY3DPEHPK3PXP";

        _mockMfaRepository.Setup(x => x.GetByUserIdAndMethodAsync(userId, method))
            .ReturnsAsync(mfaMethod);

        _mockEncryptionService.Setup(x => x.DecryptSecret(mfaMethod.EncryptedSecret, userId))
            .Returns(decryptedSecret);

        _mockTotpService.Setup(x => x.VerifyTotpWithTolerance(decryptedSecret, code, 1))
            .Returns(new TotpVerificationResult { IsValid = true });

        _mockMfaRepository.Setup(x => x.UpdateAsync(It.IsAny<UserMfaEntity>()))
            .ReturnsAsync((UserMfaEntity entity) => entity);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        _mockAuditService.Setup(x => x.CalculateRiskScoreAsync(context, userId))
            .ReturnsAsync(10);

        // Act
        var result = await _mfaService.VerifyMfaCodeAsync(userId, method, code, context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(10, result.RiskScore);
        _mockMfaRepository.Verify(x => x.UpdateAsync(It.Is<UserMfaEntity>(m => 
            m.FailedAttempts == 0 && 
            m.LockedUntil == null &&
            m.LastUsedAt != null
        )), Times.Once);
    }

    [Fact]
    public async Task VerifyMfaCodeAsync_WithInvalidCode_ShouldReturnInvalid()
    {
        // Arrange
        var userId = "user123";
        var method = "TOTP";
        var code = "000000";
        var context = new MfaVerificationContext();

        var mfaMethod = new UserMfaEntity
        {
            UserId = userId,
            Method = method,
            EncryptedSecret = "encrypted_secret",
            FailedAttempts = 0
        };

        var decryptedSecret = "JBSWY3DPEHPK3PXP";

        _mockMfaRepository.Setup(x => x.GetByUserIdAndMethodAsync(userId, method))
            .ReturnsAsync(mfaMethod);

        _mockEncryptionService.Setup(x => x.DecryptSecret(mfaMethod.EncryptedSecret, userId))
            .Returns(decryptedSecret);

        _mockTotpService.Setup(x => x.VerifyTotpWithTolerance(decryptedSecret, code, 1))
            .Returns(new TotpVerificationResult { IsValid = false });

        _mockMfaRepository.Setup(x => x.UpdateAsync(It.IsAny<UserMfaEntity>()))
            .ReturnsAsync((UserMfaEntity entity) => entity);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        _mockAuditService.Setup(x => x.CalculateRiskScoreAsync(context, userId))
            .ReturnsAsync(50);

        // Act
        var result = await _mfaService.VerifyMfaCodeAsync(userId, method, code, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(4, result.RemainingAttempts); // 5 - 1 = 4
        Assert.Equal(50, result.RiskScore);
        _mockMfaRepository.Verify(x => x.UpdateAsync(It.Is<UserMfaEntity>(m => 
            m.FailedAttempts == 1
        )), Times.Once);
    }

    [Fact]
    public async Task VerifyMfaCodeAsync_WithLockedMethod_ShouldReturnLocked()
    {
        // Arrange
        var userId = "user123";
        var method = "TOTP";
        var code = "123456";
        var context = new MfaVerificationContext();

        var mfaMethod = new UserMfaEntity
        {
            UserId = userId,
            Method = method,
            LockedUntil = DateTime.UtcNow.AddMinutes(10) // 鎖定中
        };

        _mockMfaRepository.Setup(x => x.GetByUserIdAndMethodAsync(userId, method))
            .ReturnsAsync(mfaMethod);

        // Act
        var result = await _mfaService.VerifyMfaCodeAsync(userId, method, code, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.IsLocked);
        Assert.NotNull(result.LockedUntil);
        Assert.Equal("MFA method is temporarily locked due to too many failed attempts", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task VerifyMfaCodeAsync_WithInvalidUserId_ShouldThrowArgumentException(string? userId)
    {
        // Arrange
        var method = "TOTP";
        var code = "123456";
        var context = new MfaVerificationContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _mfaService.VerifyMfaCodeAsync(userId ?? "", method, code, context));
    }

    [Fact]
    public async Task GetPrimaryMfaMethodAsync_WithExistingPrimaryMethod_ShouldReturnMethod()
    {
        // Arrange
        var userId = "user123";
        var primaryMethod = new UserMfaEntity
        {
            UserId = userId,
            Method = "TOTP",
            IsPrimary = true
        };

        _mockMfaRepository.Setup(x => x.GetPrimaryMfaMethodAsync(userId))
            .ReturnsAsync(primaryMethod);

        // Act
        var result = await _mfaService.GetPrimaryMfaMethodAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TOTP", result.Method);
        Assert.True(result.IsPrimary);
    }

    [Fact]
    public async Task GetUserMfaMethodsAsync_WithMultipleMethods_ShouldReturnAllMethods()
    {
        // Arrange
        var userId = "user123";
        var methods = new List<UserMfaEntity>
        {
            new() { UserId = userId, Method = "TOTP", IsPrimary = true },
            new() { UserId = userId, Method = "SMS", IsPrimary = false }
        };

        _mockMfaRepository.Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(methods);

        // Act
        var result = await _mfaService.GetUserMfaMethodsAsync(userId);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, m => m.Method == "TOTP" && m.IsPrimary);
        Assert.Contains(result, m => m.Method == "SMS" && !m.IsPrimary);
    }

    [Fact]
    public async Task ResetMfaAsync_ShouldDisableAllMethodsAndRevokeBackupCodes()
    {
        // Arrange
        var userId = "user123";
        var adminUserId = "admin456";
        var reason = "User locked out";

        var userMethods = new List<UserMfaEntity>
        {
            new() { Id = "mfa1", UserId = userId, Method = "TOTP", IsEnabled = true },
            new() { Id = "mfa2", UserId = userId, Method = "SMS", IsEnabled = true }
        };

        var user = new UserEntity
        {
            Id = userId,
            IsMfaEnabled = true
        };

        _mockMfaRepository.Setup(x => x.GetByUserIdAsync(userId))
            .ReturnsAsync(userMethods);

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _mockMfaRepository.Setup(x => x.UpdateAsync(It.IsAny<UserMfaEntity>()))
            .ReturnsAsync((UserMfaEntity entity) => entity);

        _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<UserEntity>()))
            .Returns(Task.CompletedTask);

        _mockBackupCodeService.Setup(x => x.RevokeAllBackupCodesAsync(userId, adminUserId, reason))
            .ReturnsAsync(true);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mfaService.ResetMfaAsync(userId, adminUserId, reason);

        // Assert
        Assert.True(result);
        _mockMfaRepository.Verify(x => x.UpdateAsync(It.Is<UserMfaEntity>(m => 
            !m.IsEnabled
        )), Times.Exactly(2));

        _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<UserEntity>(u => 
            !u.IsMfaEnabled && 
            u.DefaultMfaMethod == null
        )), Times.Once);

        _mockBackupCodeService.Verify(x => x.RevokeAllBackupCodesAsync(userId, adminUserId, reason), Times.Once);
    }
}