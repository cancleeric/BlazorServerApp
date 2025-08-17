using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalIdentityServer.Tests.Services;

/// <summary>
/// RefreshTokenRotationService 單元測試
/// 驗證 Token 輪替、重用偵測與安全回應機制
/// </summary>
public class RefreshTokenRotationServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepository;
    private readonly Mock<ILogger<RefreshTokenRotationService>> _mockLogger;
    private readonly RefreshTokenRotationService _service;

    public RefreshTokenRotationServiceTests()
    {
        _mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
        _mockLogger = new Mock<ILogger<RefreshTokenRotationService>>();

        _service = new RefreshTokenRotationService(
            _mockRefreshTokenRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RotateTokenAsync_WithValidToken_ShouldReturnNewToken()
    {
        // Arrange
        var oldToken = "valid_refresh_token";
        var clientId = "test_client";
        var scopes = new[] { "api", "openid" };
        var tokenFamily = "family_123";

        var existingToken = new RefreshTokenEntity
        {
            Token = oldToken,
            TokenFamily = tokenFamily,
            ClientId = clientId,
            Subject = "user_123",
            Username = "testuser",
            Email = "test@example.com",
            Scope = string.Join(" ", scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsUsed = false,
            IsRevoked = false
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(oldToken))
            .ReturnsAsync(existingToken);

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenFamilyAsync(tokenFamily))
            .ReturnsAsync(new List<RefreshTokenEntity> { existingToken });

        // Act
        var result = await _service.RotateTokenAsync(oldToken, clientId, scopes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.NewToken);
        Assert.NotEqual(oldToken, result.NewToken);
        Assert.NotNull(result.SecurityCheck);
        Assert.True(result.SecurityCheck.IsSecure);
        Assert.False(result.SecurityCheck.IsTokenReused);
        Assert.Equal(SecurityThreatLevel.None, result.SecurityCheck.ThreatLevel);

        // 驗證舊 Token 被標記為已使用
        _mockRefreshTokenRepository.Verify(x => 
            x.UpdateAsync(It.Is<RefreshTokenEntity>(t => 
                t.Token == oldToken && t.IsUsed == true)), 
            Times.Once);

        // 驗證新 Token 被創建
        _mockRefreshTokenRepository.Verify(x => 
            x.CreateAsync(It.Is<RefreshTokenEntity>(t => 
                t.TokenFamily == tokenFamily && 
                t.IsUsed == false && 
                t.ClientId == clientId)), 
            Times.Once);
    }

    [Fact]
    public async Task RotateTokenAsync_WithNonExistentToken_ShouldReturnFailure()
    {
        // Arrange
        var invalidToken = "invalid_token";
        var clientId = "test_client";
        var scopes = new[] { "api" };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(invalidToken))
            .ReturnsAsync((RefreshTokenEntity?)null);

        // Act
        var result = await _service.RotateTokenAsync(invalidToken, clientId, scopes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.NewToken);
        Assert.Contains("Token 不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task RotateTokenAsync_WithExpiredToken_ShouldReturnFailure()
    {
        // Arrange
        var expiredToken = "expired_token";
        var clientId = "test_client";
        var scopes = new[] { "api" };

        var expiredTokenEntity = new RefreshTokenEntity
        {
            Token = expiredToken,
            TokenFamily = "family_456",
            ClientId = clientId,
            Subject = "user_123",
            Username = "testuser",
            Email = "test@example.com",
            Scope = string.Join(" ", scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // 已過期
            IsUsed = false,
            IsRevoked = false
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(expiredToken))
            .ReturnsAsync(expiredTokenEntity);

        // Act
        var result = await _service.RotateTokenAsync(expiredToken, clientId, scopes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.NewToken);
        Assert.Contains("Token 已過期", result.ErrorMessage);
    }

    [Fact]
    public async Task RotateTokenAsync_WithUsedToken_ShouldDetectReuse()
    {
        // Arrange
        var usedToken = "used_token";
        var clientId = "test_client";
        var scopes = new[] { "api" };
        var tokenFamily = "family_789";

        var usedTokenEntity = new RefreshTokenEntity
        {
            Token = usedToken,
            TokenFamily = tokenFamily,
            ClientId = clientId,
            Subject = "user_123",
            Username = "testuser",
            Email = "test@example.com",
            Scope = string.Join(" ", scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsUsed = true, // 已使用
            IsRevoked = false
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(usedToken))
            .ReturnsAsync(usedTokenEntity);

        // Act
        var result = await _service.RotateTokenAsync(usedToken, clientId, scopes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.NewToken);
        Assert.NotNull(result.SecurityCheck);
        Assert.False(result.SecurityCheck.IsSecure);
        Assert.True(result.SecurityCheck.IsTokenReused);
        Assert.Equal(SecurityThreatLevel.High, result.SecurityCheck.ThreatLevel);
        Assert.Contains("Token 重用偵測", result.SecurityCheck.ThreatDescription);

        // 驗證所有家族 Token 被撤銷
        _mockRefreshTokenRepository.Verify(x => 
            x.RevokeTokenFamilyAsync(tokenFamily, It.IsAny<string>()), 
            Times.Once);
    }

    [Fact]
    public async Task RotateTokenAsync_WithClientMismatch_ShouldReturnFailure()
    {
        // Arrange
        var token = "valid_token";
        var requestClientId = "client_a";
        var tokenClientId = "client_b"; // 不同的客戶端
        var scopes = new[] { "api" };

        var tokenEntity = new RefreshTokenEntity
        {
            Token = token,
            TokenFamily = "family_abc",
            ClientId = tokenClientId,
            Subject = "user_123",
            Username = "testuser",
            Email = "test@example.com",
            Scope = string.Join(" ", scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsUsed = false,
            IsRevoked = false
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(tokenEntity);

        // Act
        var result = await _service.RotateTokenAsync(token, requestClientId, scopes);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.NewToken);
        Assert.Contains("Token 與客戶端不匹配", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateTokenFamilyAsync_ShouldCreateNewTokenWithFamily()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = "user_123",
            UserName = "testuser",
            Email = "test@example.com",
            Roles = "User"
        };

        var client = new ClientEntity
        {
            ClientId = "test_client",
            ClientName = "Test Client",
            IsActive = true
        };

        var scopes = new[] { "openid", "profile", "api" };

        // Act
        var result = await _service.CreateTokenFamilyAsync(user, client, scopes);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.TokenFamily);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);

        // 驗證新 Token 被創建
        _mockRefreshTokenRepository.Verify(x => 
            x.CreateAsync(It.Is<RefreshTokenEntity>(t => 
                t.TokenFamily == result.TokenFamily && 
                t.Subject == user.Id &&
                t.ClientId == client.ClientId &&
                t.Scope == string.Join(" ", scopes))), 
            Times.Once);
    }

    [Fact]
    public async Task CheckTokenReuseAsync_WithValidToken_ShouldReturnSecure()
    {
        // Arrange
        var token = "valid_token";
        var tokenEntity = new RefreshTokenEntity
        {
            Token = token,
            TokenFamily = "family_123",
            IsUsed = false,
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(tokenEntity);

        // Act
        var result = await _service.CheckTokenReuseAsync(token);

        // Assert
        Assert.True(result.IsSecure);
        Assert.False(result.IsTokenReused);
        Assert.Equal(SecurityThreatLevel.None, result.ThreatLevel);
    }

    [Fact]
    public async Task CheckTokenReuseAsync_WithReusedToken_ShouldDetectThreat()
    {
        // Arrange
        var token = "reused_token";
        var tokenEntity = new RefreshTokenEntity
        {
            Token = token,
            TokenFamily = "family_456",
            IsUsed = true, // Token 已被使用
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(tokenEntity);

        // Act
        var result = await _service.CheckTokenReuseAsync(token);

        // Assert
        Assert.False(result.IsSecure);
        Assert.True(result.IsTokenReused);
        Assert.Equal(SecurityThreatLevel.High, result.ThreatLevel);
        Assert.Contains("Token 重用", result.ThreatDescription);
    }

    [Fact]
    public async Task EmergencyRevokeTokenFamilyAsync_ShouldRevokeAllFamilyTokens()
    {
        // Arrange
        var tokenFamily = "family_emergency";
        var reason = "Security breach detected";

        // Act
        await _service.EmergencyRevokeTokenFamilyAsync(tokenFamily, reason);

        // Assert
        _mockRefreshTokenRepository.Verify(x => 
            x.RevokeTokenFamilyAsync(tokenFamily, It.IsAny<string>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetTokenFamilyStatisticsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var tokenFamily = "family_stats";
        var familyTokens = new[]
        {
            new RefreshTokenEntity
            {
                TokenFamily = tokenFamily,
                IsUsed = false,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new RefreshTokenEntity
            {
                TokenFamily = tokenFamily,
                IsUsed = true,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UsedAt = DateTime.UtcNow.AddDays(-1)
            },
            new RefreshTokenEntity
            {
                TokenFamily = tokenFamily,
                IsUsed = false,
                IsRevoked = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenFamilyAsync(tokenFamily))
            .ReturnsAsync(familyTokens.ToList());

        // Act
        var stats = await _service.GetTokenFamilyStatisticsAsync(tokenFamily);

        // Assert
        Assert.Equal(tokenFamily, stats.TokenFamily);
        Assert.Equal(3, stats.TotalTokens);
        Assert.Equal(1, stats.ActiveTokens); // 未使用且未撤銷
        Assert.Equal(1, stats.UsedTokens);
        Assert.Equal(1, stats.RevokedTokens);
        Assert.True(stats.FamilyAge.TotalDays >= 5);
        Assert.NotNull(stats.LastUsedAt);
    }

    [Theory]
    [InlineData("", "test_client", new[] { "api" })]
    [InlineData("valid_token", "", new[] { "api" })]
    [InlineData("valid_token", "test_client", new string[0])]
    public async Task RotateTokenAsync_WithInvalidParameters_ShouldReturnFailure(
        string token, string clientId, string[] scopes)
    {
        // Act
        var result = await _service.RotateTokenAsync(token, clientId, scopes);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("參數無效", result.ErrorMessage);
    }

    [Fact]
    public async Task RotateTokenAsync_WithConcurrentUsage_ShouldHandleGracefully()
    {
        // Arrange
        var token = "concurrent_token";
        var clientId = "test_client";
        var scopes = new[] { "api" };
        var tokenFamily = "family_concurrent";

        var tokenEntity = new RefreshTokenEntity
        {
            Token = token,
            TokenFamily = tokenFamily,
            ClientId = clientId,
            Subject = "user_123",
            Username = "testuser",
            Email = "test@example.com",
            Scope = string.Join(" ", scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsUsed = false,
            IsRevoked = false
        };

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(tokenEntity);

        _mockRefreshTokenRepository
            .Setup(x => x.GetByTokenFamilyAsync(tokenFamily))
            .ReturnsAsync(new List<RefreshTokenEntity> { tokenEntity });

        // 模擬併發更新時的異常
        _mockRefreshTokenRepository
            .Setup(x => x.UpdateAsync(It.IsAny<RefreshTokenEntity>()))
            .ThrowsAsync(new InvalidOperationException("Concurrent update detected"));

        // Act
        var result = await _service.RotateTokenAsync(token, clientId, scopes);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("併發使用", result.ErrorMessage);
    }
}