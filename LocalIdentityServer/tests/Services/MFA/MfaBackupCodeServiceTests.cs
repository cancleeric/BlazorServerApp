using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Services.MFA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalIdentityServer.Tests.Services.MFA;

/// <summary>
/// MFA 備援代碼服務單元測試
/// 測試備援代碼的生成、驗證和管理功能
/// 確保備援代碼的安全性和一次性使用特性
/// </summary>
public class MfaBackupCodeServiceTests
{
    private readonly Mock<IMfaBackupCodeRepository> _mockBackupCodeRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IMfaAuditService> _mockAuditService;
    private readonly Mock<ILogger<MfaBackupCodeService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly MfaBackupCodeService _backupCodeService;

    public MfaBackupCodeServiceTests()
    {
        _mockBackupCodeRepository = new Mock<IMfaBackupCodeRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuditService = new Mock<IMfaAuditService>();
        _mockLogger = new Mock<ILogger<MfaBackupCodeService>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // 設定預設配置
        _mockConfiguration.Setup(c => c["Mfa:BackupCodes:Length"]).Returns("8");
        _mockConfiguration.Setup(c => c["Mfa:BackupCodes:ExpirationDays"]).Returns("90");

        _backupCodeService = new MfaBackupCodeService(
            _mockBackupCodeRepository.Object,
            _mockUserRepository.Object,
            _mockAuditService.Object,
            _mockLogger.Object,
            _mockConfiguration.Object
        );
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_WithValidInput_ShouldReturnCodes()
    {
        // Arrange
        var userId = "user123";
        var count = 10;

        var user = new UserEntity { Id = userId, UserName = "testuser" };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesAsync(userId))
            .ReturnsAsync(new List<MfaBackupCodeEntity>());

        _mockBackupCodeRepository.Setup(x => x.CreateAsync(It.IsAny<MfaBackupCodeEntity>()))
            .ReturnsAsync((MfaBackupCodeEntity entity) => entity);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _backupCodeService.GenerateBackupCodesAsync(userId, count);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(count, result.BackupCodes.Length);
        Assert.All(result.BackupCodes, code => 
        {
            Assert.NotNull(code);
            Assert.Equal(8, code.Length);
            Assert.Matches("^[0-9]+$", code); // 只包含數字
        });

        _mockBackupCodeRepository.Verify(x => x.CreateAsync(It.IsAny<MfaBackupCodeEntity>()), Times.Exactly(count));
    }

    [Fact]
    public async Task GenerateBackupCodesAsync_WithNonExistentUser_ShouldReturnFailure()
    {
        // Arrange
        var userId = "nonexistent";

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _backupCodeService.GenerateBackupCodesAsync(userId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("User not found", result.ErrorMessage);
        Assert.Empty(result.BackupCodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GenerateBackupCodesAsync_WithInvalidUserId_ShouldReturnFailure(string? userId)
    {
        // Act
        var result = await _backupCodeService.GenerateBackupCodesAsync(userId ?? "");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("User ID cannot be null or empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)] // 超過最大限制
    public async Task GenerateBackupCodesAsync_WithInvalidCount_ShouldReturnFailure(int count)
    {
        // Arrange
        var userId = "user123";

        // Act
        var result = await _backupCodeService.GenerateBackupCodesAsync(userId, count);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Backup code count must be between 1 and 20", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithValidCode_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "user123";
        var code = "12345678";
        var context = new MfaVerificationContext { IpAddress = "192.168.1.1" };

        var backupCode = new MfaBackupCodeEntity
        {
            Id = "backup123",
            UserId = userId,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesAsync(userId))
            .ReturnsAsync(new List<MfaBackupCodeEntity> { backupCode });

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesCountAsync(userId))
            .ReturnsAsync(5);

        _mockBackupCodeRepository.Setup(x => x.UpdateAsync(It.IsAny<MfaBackupCodeEntity>()))
            .ReturnsAsync((MfaBackupCodeEntity entity) => entity);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _backupCodeService.VerifyBackupCodeAsync(userId, code, context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);

        _mockBackupCodeRepository.Verify(x => x.UpdateAsync(It.Is<MfaBackupCodeEntity>(bc => 
            bc.IsUsed && 
            bc.UsedAt != null
        )), Times.Once);
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithInvalidCode_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user123";
        var code = "87654321";
        var context = new MfaVerificationContext();

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesAsync(userId))
            .ReturnsAsync(new List<MfaBackupCodeEntity>());

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _backupCodeService.VerifyBackupCodeAsync(userId, code, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid or expired backup code", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyBackupCodeAsync_WithUsedCode_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user123";
        var code = "12345678";
        var context = new MfaVerificationContext();

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesAsync(userId))
            .ReturnsAsync(new List<MfaBackupCodeEntity>()); // 已使用的代碼不會被返回

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _backupCodeService.VerifyBackupCodeAsync(userId, code, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid or expired backup code", result.ErrorMessage);
    }

    [Fact]
    public async Task GetBackupCodeStatusAsync_WithExistingCodes_ShouldReturnCorrectStatus()
    {
        // Arrange
        var userId = "user123";
        var backupCodes = new List<MfaBackupCodeEntity>
        {
            new()
            {
                Id = "backup1",
                UserId = userId,
                CodeHash = "hash1",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = "backup2",
                UserId = userId,
                CodeHash = "hash2",
                IsUsed = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockBackupCodeRepository.Setup(x => x.GetUserBackupCodesAsync(userId))
            .ReturnsAsync(backupCodes);

        // Act
        var result = await _backupCodeService.GetBackupCodeStatusAsync(userId);

        // Assert
        Assert.Equal(2, result.TotalGenerated);
        Assert.Equal(1, result.AvailableCodes);
        Assert.Equal(1, result.UsedCodes);
    }

    [Fact]
    public async Task RevokeAllBackupCodesAsync_WithExistingCodes_ShouldMarkAsUsed()
    {
        // Arrange
        var userId = "user123";
        var revokedByUserId = "admin456";
        var reason = "Security reset";

        var backupCodes = new List<MfaBackupCodeEntity>
        {
            new()
            {
                Id = "backup1",
                UserId = userId,
                IsUsed = false
            },
            new()
            {
                Id = "backup2",
                UserId = userId,
                IsUsed = false
            }
        };

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesAsync(userId))
            .ReturnsAsync(backupCodes);

        _mockBackupCodeRepository.Setup(x => x.UpdateAsync(It.IsAny<MfaBackupCodeEntity>()))
            .ReturnsAsync((MfaBackupCodeEntity entity) => entity);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _backupCodeService.RevokeAllBackupCodesAsync(userId, revokedByUserId, reason);

        // Assert
        Assert.True(result);
        _mockBackupCodeRepository.Verify(x => x.UpdateAsync(It.Is<MfaBackupCodeEntity>(bc => 
            bc.IsUsed
        )), Times.Exactly(2));
    }

    [Fact]
    public async Task GetBackupCodeStatusAsync_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var userId = "user123";
        var backupCodes = new List<MfaBackupCodeEntity>
        {
            new() { UserId = userId, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow },
            new() { UserId = userId, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow },
            new() { UserId = userId, IsUsed = true, ExpiresAt = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow, UsedAt = DateTime.UtcNow.AddHours(-1) },
            new() { UserId = userId, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow } // 過期
        };

        _mockBackupCodeRepository.Setup(x => x.GetUserBackupCodesAsync(userId))
            .ReturnsAsync(backupCodes);

        // Act
        var statistics = await _backupCodeService.GetBackupCodeStatusAsync(userId);

        // Assert
        Assert.Equal(4, statistics.TotalGenerated);
        Assert.Equal(2, statistics.AvailableCodes); // 未使用且未過期
        Assert.Equal(1, statistics.UsedCodes);
        Assert.True(statistics.HasExpiredCodes);
        Assert.NotNull(statistics.LastGeneratedAt);
        Assert.NotNull(statistics.LastUsedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1234567")] // 太短
    [InlineData("123456789")] // 太長
    [InlineData("abcd1234")] // 包含字母
    public async Task VerifyBackupCodeAsync_WithInvalidCodeFormat_ShouldReturnFailure(string? code)
    {
        // Arrange
        var userId = "user123";
        var context = new MfaVerificationContext();

        // Act
        var result = await _backupCodeService.VerifyBackupCodeAsync(userId, code ?? "", context);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task BackupCodeService_ShouldEnsureUniqueCodesPerUser()
    {
        // Arrange
        var userId = "user123";
        var user = new UserEntity { Id = userId, UserName = "testuser" };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _mockBackupCodeRepository.Setup(x => x.GetAvailableBackupCodesAsync(userId))
            .ReturnsAsync(new List<MfaBackupCodeEntity>());

        _mockBackupCodeRepository.Setup(x => x.CreateAsync(It.IsAny<MfaBackupCodeEntity>()))
            .ReturnsAsync((MfaBackupCodeEntity entity) => entity);

        _mockAuditService.Setup(x => x.LogMfaEventAsync(It.IsAny<MfaAuditEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _backupCodeService.GenerateBackupCodesAsync(userId, 20); // Max allowed is 20

        // Assert
        Assert.True(result.Success);
        Assert.Equal(20, result.BackupCodes.Distinct().Count()); // 所有代碼都應該是唯一的
    }

    private static string GenerateRandomCode(int length)
    {
        var random = new Random();
        var code = "";
        for (int i = 0; i < length; i++)
        {
            code += random.Next(0, 10).ToString();
        }
        return code;
    }
}