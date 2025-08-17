using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Data.Repositories;
using EnterpriseIDS.Infrastructure.Services;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// JWT Token 服務單元測試
/// </summary>
public class JwtTokenServiceTests
{
    private readonly Mock<IJwtTokenRepository> _mockTokenRepository;
    private readonly Mock<ITokenBlacklistRepository> _mockBlacklistRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<JwtTokenService>> _mockLogger;
    private readonly JwtTokenSettings _settings;
    private readonly JwtTokenService _jwtTokenService;

    public JwtTokenServiceTests()
    {
        _mockTokenRepository = new Mock<IJwtTokenRepository>();
        _mockBlacklistRepository = new Mock<ITokenBlacklistRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<JwtTokenService>>();

        _settings = new JwtTokenSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "ThisIsAVeryLongSecretKeyForTesting1234567890ABCDEF",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7,
            IdTokenExpirationMinutes = 60,
            ClockSkewMinutes = 5,
            EnableRefreshTokenRotation = true
        };

        var options = Options.Create(_settings);
        _jwtTokenService = new JwtTokenService(
            _mockTokenRepository.Object,
            _mockBlacklistRepository.Object,
            _mockUserRepository.Object,
            _mockLogger.Object,
            options);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnValidToken_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = CreateTestUser(userId, tenantId);

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTokenRepository.Setup(x => x.AddAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken token, CancellationToken ct) => token);

        _mockTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _jwtTokenService.GenerateAccessTokenAsync(userId, tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.NotEmpty(result.JwtId);
        Assert.Equal("access_token", result.TokenType);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.True(result.IssuedAt <= DateTime.UtcNow);

        _mockTokenRepository.Verify(x => x.AddAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldThrowException_WhenUserNotExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _jwtTokenService.GenerateAccessTokenAsync(userId, tenantId));
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldReturnValidToken_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = CreateTestUser(userId, tenantId);

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTokenRepository.Setup(x => x.AddAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken token, CancellationToken ct) => token);

        _mockTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _jwtTokenService.GenerateRefreshTokenAsync(userId, tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.NotEmpty(result.JwtId);
        Assert.Equal("refresh_token", result.TokenType);
        Assert.True(result.ExpiresAt > DateTime.UtcNow.AddDays(6)); // 應該是 7 天後
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnValid_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = CreateTestUser(userId, tenantId);

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTokenRepository.Setup(x => x.AddAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken token, CancellationToken ct) => token);

        _mockTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // 先生成一個 Token
        var tokenResult = await _jwtTokenService.GenerateAccessTokenAsync(userId, tenantId);

        // Mock Token Repository 返回
        var jwtToken = CreateTestJwtToken(userId, tenantId, tokenResult.JwtId);
        _mockTokenRepository.Setup(x => x.GetByJwtIdAsync(tokenResult.JwtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jwtToken);

        _mockBlacklistRepository.Setup(x => x.IsBlacklistedAsync(tokenResult.JwtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockTokenRepository.Setup(x => x.UpdateTokenUsageAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var validationResult = await _jwtTokenService.ValidateTokenAsync(tokenResult.Token);

        // Assert
        Assert.True(validationResult.IsValid);
        Assert.Equal(tokenResult.JwtId, validationResult.JwtId);
        Assert.Equal(userId, validationResult.UserId);
        Assert.Equal(tenantId, validationResult.TenantId);
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnInvalid_WhenTokenIsBlacklisted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = CreateTestUser(userId, tenantId);

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTokenRepository.Setup(x => x.AddAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken token, CancellationToken ct) => token);

        _mockTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // 先生成一個 Token
        var tokenResult = await _jwtTokenService.GenerateAccessTokenAsync(userId, tenantId);

        // Mock 黑名單檢查返回 true
        _mockBlacklistRepository.Setup(x => x.IsBlacklistedAsync(tokenResult.JwtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var validationResult = await _jwtTokenService.ValidateTokenAsync(tokenResult.Token);

        // Assert
        Assert.False(validationResult.IsValid);
        Assert.Equal("token_blacklisted", validationResult.ErrorCode);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnTrue_WhenTokenExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var jwtId = Guid.NewGuid().ToString("N");
        var token = "test_token";
        var tokenHash = JwtTokenRepository.ComputeTokenHash(token);

        var jwtToken = CreateTestJwtToken(userId, tenantId, jwtId);
        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jwtToken);

        _mockBlacklistRepository.Setup(x => x.AddAsync(It.IsAny<TokenBlacklist>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TokenBlacklist bl, CancellationToken ct) => bl);

        _mockTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockBlacklistRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _jwtTokenService.RevokeTokenAsync(token, "Test revocation");

        // Assert
        Assert.True(result);
        Assert.Equal(TokenStatus.Revoked, jwtToken.Status);
        Assert.NotNull(jwtToken.RevokedAt);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnFalse_WhenTokenNotExists()
    {
        // Arrange
        var token = "non_existent_token";
        var tokenHash = JwtTokenRepository.ComputeTokenHash(token);

        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken?)null);

        // Act
        var result = await _jwtTokenService.RevokeTokenAsync(token, "Test revocation");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var refreshToken = "valid_refresh_token";
        var tokenHash = JwtTokenRepository.ComputeTokenHash(refreshToken);

        var user = CreateTestUser(userId, tenantId);
        var refreshTokenEntity = CreateTestJwtToken(userId, tenantId, Guid.NewGuid().ToString("N"), "refresh_token");

        _mockTokenRepository.Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshTokenEntity);

        _mockBlacklistRepository.Setup(x => x.IsBlacklistedAsync(refreshTokenEntity.JwtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockTokenRepository.Setup(x => x.AddAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken token, CancellationToken ct) => 
            {
                token.Id = Guid.NewGuid();
                return token;
            });

        _mockTokenRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken ct) => 
            {
                var token = CreateTestJwtToken(userId, tenantId, Guid.NewGuid().ToString("N"));
                token.Id = id;
                return token;
            });

        _mockTokenRepository.Setup(x => x.UpdateAsync(It.IsAny<JwtToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JwtToken token, CancellationToken ct) => token);

        _mockTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _jwtTokenService.RefreshAccessTokenAsync(refreshToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessToken);
        Assert.NotEmpty(result.AccessToken.Token);
        Assert.Equal("access_token", result.AccessToken.TokenType);

        if (_settings.EnableRefreshTokenRotation)
        {
            Assert.True(result.RefreshTokenRotated);
            Assert.NotNull(result.RefreshToken);
        }
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_ShouldReturnTrue_WhenTokenIsBlacklisted()
    {
        // Arrange
        var jwtId = "test_jwt_id";
        _mockBlacklistRepository.Setup(x => x.IsBlacklistedAsync(jwtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _jwtTokenService.IsTokenBlacklistedAsync(jwtId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetTokenStatisticsAsync_ShouldReturnStatistics()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var statistics = new TokenStatistics
        {
            TotalTokens = 10,
            ActiveTokens = 8,
            ExpiredTokens = 1,
            RevokedTokens = 1,
            AccessTokens = 5,
            RefreshTokens = 3,
            IdTokens = 2
        };

        var blacklistStats = new BlacklistStatistics
        {
            TotalBlacklistItems = 5
        };

        _mockTokenRepository.Setup(x => x.GetTokenStatisticsAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        _mockBlacklistRepository.Setup(x => x.GetBlacklistStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(blacklistStats);

        // Act
        var result = await _jwtTokenService.GetTokenStatisticsAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalTokens);
        Assert.Equal(8, result.ActiveTokens);
        Assert.Equal(5, result.BlacklistItems);
    }

    #region 輔助方法

    private static User CreateTestUser(Guid userId, Guid tenantId)
    {
        return new User
        {
            Id = userId,
            TenantId = tenantId,
            Username = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static JwtToken CreateTestJwtToken(Guid userId, Guid tenantId, string jwtId, string tokenType = "access_token")
    {
        return new JwtToken
        {
            Id = Guid.NewGuid(),
            JwtId = jwtId,
            UserId = userId,
            TenantId = tenantId,
            TokenType = tokenType,
            TokenValue = "hashed_token_value",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Subject = userId.ToString(),
            Status = TokenStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}