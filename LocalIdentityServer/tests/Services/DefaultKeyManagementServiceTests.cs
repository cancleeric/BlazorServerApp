using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;
using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Services;

namespace LocalIdentityServer.Tests.Services;

/// <summary>
/// DefaultKeyManagementService 單元測試
/// 驗證金鑰管理服務的核心功能
/// </summary>
public class DefaultKeyManagementServiceTests
{
    private readonly Mock<IPersistedKeyRepository> _mockKeyRepository;
    private readonly Mock<IKeyDataProtectionService> _mockKeyProtectionService;
    private readonly Mock<ILogger<DefaultKeyManagementService>> _mockLogger;
    private readonly KeyManagementOptions _options;
    private readonly DefaultKeyManagementService _keyManagementService;

    public DefaultKeyManagementServiceTests()
    {
        _mockKeyRepository = new Mock<IPersistedKeyRepository>();
        _mockKeyProtectionService = new Mock<IKeyDataProtectionService>();
        _mockLogger = new Mock<ILogger<DefaultKeyManagementService>>();
        _options = new KeyManagementOptions
        {
            DefaultKeySize = 2048,
            DefaultAlgorithm = "RS256",
            DefaultKeyValidityDays = 365,
            RotationWarningDays = 30,
            MinimumKeyCount = 2
        };

        // 設置 Mock 加密服務行為
        _mockKeyProtectionService
            .Setup(x => x.ProtectKeyData(It.IsAny<string>()))
            .Returns<string>(data => $"ENCRYPTED_{data}");
            
        _mockKeyProtectionService
            .Setup(x => x.UnprotectKeyData(It.IsAny<string>()))
            .Returns<string>(encryptedData => encryptedData.Replace("ENCRYPTED_", ""));

        var optionsWrapper = Options.Create(_options);
        _keyManagementService = new DefaultKeyManagementService(
            _mockKeyRepository.Object,
            _mockKeyProtectionService.Object,
            _mockLogger.Object,
            optionsWrapper);
    }

    [Fact]
    public async Task GetCurrentSigningKeyAsync_WhenPrimaryKeyExists_ReturnsSecurityKey()
    {
        // Arrange
        var keyEntity = CreateTestKeyEntity();
        keyEntity.IsPrimary = true;

        _mockKeyRepository
            .Setup(x => x.GetPrimarySigningKeyAsync())
            .ReturnsAsync(keyEntity);

        // Act
        var result = await _keyManagementService.GetCurrentSigningKeyAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(keyEntity.KeyId, result.KeyId);
        Assert.IsType<RsaSecurityKey>(result);

        _mockKeyRepository.Verify(x => x.GetPrimarySigningKeyAsync(), Times.Once);
    }

    [Fact]
    public async Task GetCurrentSigningKeyAsync_WhenNoPrimaryKey_ReturnsNull()
    {
        // Arrange
        _mockKeyRepository
            .Setup(x => x.GetPrimarySigningKeyAsync())
            .ReturnsAsync((PersistedKeyEntity?)null);

        // Act
        var result = await _keyManagementService.GetCurrentSigningKeyAsync();

        // Assert
        Assert.Null(result);

        _mockKeyRepository.Verify(x => x.GetPrimarySigningKeyAsync(), Times.Once);
    }

    [Fact]
    public async Task GetValidationKeysAsync_ReturnsAllValidKeys()
    {
        // Arrange
        var keyEntities = new List<PersistedKeyEntity>
        {
            CreateTestKeyEntity("key1"),
            CreateTestKeyEntity("key2"),
            CreateTestKeyEntity("key3")
        };

        _mockKeyRepository
            .Setup(x => x.GetValidSigningKeysAsync())
            .ReturnsAsync(keyEntities);

        // Act
        var result = await _keyManagementService.GetValidationKeysAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
        Assert.All(result, key => Assert.IsType<RsaSecurityKey>(key));

        _mockKeyRepository.Verify(x => x.GetValidSigningKeysAsync(), Times.Once);
    }

    [Theory]
    [InlineData(2048, "RS256")]
    [InlineData(4096, "RS384")]
    [InlineData(2048, "RS512")]
    public async Task GenerateNewKeyAsync_WithValidParameters_ReturnsKeyId(int keySize, string algorithm)
    {
        // Arrange
        _mockKeyRepository
            .Setup(x => x.StoreAsync(It.IsAny<PersistedKeyEntity>()))
            .ReturnsAsync((PersistedKeyEntity key) => key);

        // Act
        var result = await _keyManagementService.GenerateNewKeyAsync(keySize, algorithm);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        _mockKeyRepository.Verify(
            x => x.StoreAsync(It.Is<PersistedKeyEntity>(k => 
                k.Algorithm == algorithm && 
                k.KeyType == "RSA" && 
                k.Use == "sig")), 
            Times.Once);
    }

    [Theory]
    [InlineData(1024)] // 太小
    [InlineData(3072)] // 不標準大小
    public async Task GenerateNewKeyAsync_WithInvalidKeySize_ThrowsArgumentException(int invalidKeySize)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _keyManagementService.GenerateNewKeyAsync(invalidKeySize));

        Assert.Contains("Key size must be 2048 or 4096", exception.Message);
    }

    [Theory]
    [InlineData("HS256")] // 不支援的對稱算法
    [InlineData("ES256")] // 不支援的 ECDSA 算法
    [InlineData("INVALID")] // 無效算法
    public async Task GenerateNewKeyAsync_WithInvalidAlgorithm_ThrowsArgumentException(string invalidAlgorithm)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _keyManagementService.GenerateNewKeyAsync(2048, invalidAlgorithm));

        Assert.Contains($"Unsupported algorithm: {invalidAlgorithm}", exception.Message);
    }

    [Fact]
    public async Task SetPrimaryKeyAsync_WithValidKeyId_CallsRepository()
    {
        // Arrange
        var keyId = "test-key-id";

        _mockKeyRepository
            .Setup(x => x.SetPrimaryKeyAsync(keyId))
            .Returns(Task.CompletedTask);

        // Act
        await _keyManagementService.SetPrimaryKeyAsync(keyId);

        // Assert
        _mockKeyRepository.Verify(x => x.SetPrimaryKeyAsync(keyId), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetPrimaryKeyAsync_WithInvalidKeyId_ThrowsArgumentException(string invalidKeyId)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _keyManagementService.SetPrimaryKeyAsync(invalidKeyId));

        Assert.Contains("Key ID cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task RevokeKeyAsync_WithValidKeyId_CallsRepository()
    {
        // Arrange
        var keyId = "test-key-id";
        var reason = "Security compromise";

        _mockKeyRepository
            .Setup(x => x.RevokeKeyAsync(keyId))
            .Returns(Task.CompletedTask);

        // Act
        await _keyManagementService.RevokeKeyAsync(keyId, reason);

        // Assert
        _mockKeyRepository.Verify(x => x.RevokeKeyAsync(keyId), Times.Once);
    }

    [Fact]
    public async Task RotateKeyIfNeededAsync_WhenRotationNeeded_PerformsRotation()
    {
        // Arrange
        _mockKeyRepository
            .Setup(x => x.ShouldRotateKeyAsync())
            .ReturnsAsync(true);

        _mockKeyRepository
            .Setup(x => x.StoreAsync(It.IsAny<PersistedKeyEntity>()))
            .ReturnsAsync((PersistedKeyEntity key) => key);

        _mockKeyRepository
            .Setup(x => x.SetPrimaryKeyAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _keyManagementService.RotateKeyIfNeededAsync();

        // Assert
        Assert.True(result);

        _mockKeyRepository.Verify(x => x.ShouldRotateKeyAsync(), Times.Once);
        _mockKeyRepository.Verify(x => x.StoreAsync(It.IsAny<PersistedKeyEntity>()), Times.Once);
        _mockKeyRepository.Verify(x => x.SetPrimaryKeyAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RotateKeyIfNeededAsync_WhenRotationNotNeeded_DoesNotRotate()
    {
        // Arrange
        _mockKeyRepository
            .Setup(x => x.ShouldRotateKeyAsync())
            .ReturnsAsync(false);

        // Act
        var result = await _keyManagementService.RotateKeyIfNeededAsync();

        // Assert
        Assert.False(result);

        _mockKeyRepository.Verify(x => x.ShouldRotateKeyAsync(), Times.Once);
        _mockKeyRepository.Verify(x => x.StoreAsync(It.IsAny<PersistedKeyEntity>()), Times.Never);
        _mockKeyRepository.Verify(x => x.SetPrimaryKeyAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetKeyStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var validKeys = new List<PersistedKeyEntity>
        {
            CreateTestKeyEntity("key1", isActive: true, isPrimary: true, expiresAt: now.AddDays(60)),
            CreateTestKeyEntity("key2", isActive: true, isPrimary: false, expiresAt: now.AddDays(30)),
            CreateTestKeyEntity("key3", isActive: false, isPrimary: false, expiresAt: now.AddDays(-10)), // 過期
            CreateTestKeyEntity("key4", isActive: true, isPrimary: false, isRevoked: true) // 撤銷
        };

        var primaryKey = validKeys.First(k => k.IsPrimary);

        _mockKeyRepository
            .Setup(x => x.GetValidSigningKeysAsync())
            .ReturnsAsync(validKeys);

        _mockKeyRepository
            .Setup(x => x.GetPrimarySigningKeyAsync())
            .ReturnsAsync(primaryKey);

        // Act
        var result = await _keyManagementService.GetKeyStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalKeys);
        Assert.Equal(2, result.ActiveKeys); // key1, key2 (未過期且未撤銷)
        Assert.Equal(1, result.ExpiredKeys); // key3
        Assert.Equal(1, result.RevokedKeys); // key4
        Assert.NotNull(result.PrimaryKey);
        Assert.Equal("key1", result.PrimaryKey.KeyId);
    }

    [Fact]
    public async Task CheckKeyHealthAsync_WithNoPrimaryKey_ReturnsUnhealthy()
    {
        // Arrange
        _mockKeyRepository
            .Setup(x => x.GetPrimarySigningKeyAsync())
            .ReturnsAsync((PersistedKeyEntity?)null);

        _mockKeyRepository
            .Setup(x => x.GetValidSigningKeysAsync())
            .ReturnsAsync(new List<PersistedKeyEntity>());

        // Act
        var result = await _keyManagementService.CheckKeyHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Contains("No primary signing key found", result.Issues);
    }

    [Fact]
    public async Task CheckKeyHealthAsync_WithExpiringKey_ReturnsWarning()
    {
        // Arrange
        var expiringKey = CreateTestKeyEntity("expiring-key", isPrimary: true, 
            expiresAt: DateTime.UtcNow.AddDays(15)); // 15天後過期，小於預設30天警告期

        _mockKeyRepository
            .Setup(x => x.GetPrimarySigningKeyAsync())
            .ReturnsAsync(expiringKey);

        _mockKeyRepository
            .Setup(x => x.GetValidSigningKeysAsync())
            .ReturnsAsync(new List<PersistedKeyEntity> { expiringKey, CreateTestKeyEntity("backup-key") });

        // Act
        var result = await _keyManagementService.CheckKeyHealthAsync();

        // Assert
        Assert.True(result.IsHealthy); // 有主要金鑰所以健康
        Assert.Single(result.Warnings);
        Assert.Contains("Primary key will expire in", result.Warnings.First());
    }

    #region Helper Methods

    private static PersistedKeyEntity CreateTestKeyEntity(
        string keyId = "test-key",
        bool isActive = true,
        bool isPrimary = false,
        bool isRevoked = false,
        DateTime? expiresAt = null)
    {
        // 為測試生成真正的 RSA 金鑰對
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        var publicKeyBytes = rsa.ExportRSAPublicKey();
        
        var privateKeyBase64 = Convert.ToBase64String(privateKeyBytes);
        var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);

        return new PersistedKeyEntity
        {
            Id = 1,
            KeyId = keyId,
            Algorithm = "RS256",
            Use = "sig",
            KeyType = "RSA",
            EncryptedKeyData = $"ENCRYPTED_{privateKeyBase64}", // 模擬加密後的資料
            PublicKeyData = publicKeyBase64,
            Version = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastModifiedAt = DateTime.UtcNow.AddDays(-10),
            ActivatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(365),
            IsPrimary = isPrimary,
            IsRevoked = isRevoked,
            IsDeleted = false
        };
    }

    #endregion
}