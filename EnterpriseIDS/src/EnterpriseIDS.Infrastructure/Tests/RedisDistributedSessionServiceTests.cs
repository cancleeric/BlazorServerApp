using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Caching;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// Redis 分散式會話服務測試
/// </summary>
public class RedisDistributedSessionServiceTests
{
    private readonly Mock<ILogger<RedisDistributedSessionService>> _loggerMock;
    private readonly IDistributedCache _distributedCache;
    private readonly RedisDistributedSessionService _sessionService;

    public RedisDistributedSessionServiceTests()
    {
        _loggerMock = new Mock<ILogger<RedisDistributedSessionService>>();
        
        // 使用記憶體快取進行測試
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        
        _sessionService = new RedisDistributedSessionService(_distributedCache, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldCreateSession_WhenValidParameters()
    {
        // Arrange
        var sessionId = "test_session_123";
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var metadata = new Dictionary<string, object> { { "test", "value" } };

        // Act
        await _sessionService.CreateSessionAsync(sessionId, userId, tenantId, metadata);

        // Assert
        var sessionInfo = await _sessionService.GetSessionAsync(sessionId);
        Assert.NotNull(sessionInfo);
        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal(userId, sessionInfo.UserId);
        Assert.Equal(tenantId, sessionInfo.TenantId);
        Assert.Equal("value", sessionInfo.Metadata["test"]);
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnNull_WhenSessionNotExists()
    {
        // Arrange
        var sessionId = "non_existent_session";

        // Act
        var sessionInfo = await _sessionService.GetSessionAsync(sessionId);

        // Assert
        Assert.Null(sessionInfo);
    }

    [Fact]
    public async Task RefreshSessionAsync_ShouldUpdateLastActivityTime()
    {
        // Arrange
        var sessionId = "test_session_refresh";
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await _sessionService.CreateSessionAsync(sessionId, userId, tenantId);
        
        var originalSession = await _sessionService.GetSessionAsync(sessionId);
        var originalLastActivity = originalSession!.LastActivityAt;

        // 等待確保時間不同
        await Task.Delay(100);

        // Act
        await _sessionService.RefreshSessionAsync(sessionId);

        // Assert
        var refreshedSession = await _sessionService.GetSessionAsync(sessionId);
        Assert.NotNull(refreshedSession);
        Assert.True(refreshedSession.LastActivityAt > originalLastActivity);
    }

    [Fact]
    public async Task SetSessionDataAsync_ShouldStoreData_WhenSessionExists()
    {
        // Arrange
        var sessionId = "test_session_data";
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataKey = "test_key";
        var dataValue = new { Name = "Test", Value = 123 };

        await _sessionService.CreateSessionAsync(sessionId, userId, tenantId);

        // Act
        await _sessionService.SetSessionDataAsync(sessionId, dataKey, dataValue);

        // Assert
        var retrievedData = await _sessionService.GetSessionDataAsync<object>(sessionId, dataKey);
        Assert.NotNull(retrievedData);
    }

    [Fact]
    public async Task GetSessionDataAsync_ShouldReturnDefault_WhenDataNotExists()
    {
        // Arrange
        var sessionId = "test_session_no_data";
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataKey = "non_existent_key";

        await _sessionService.CreateSessionAsync(sessionId, userId, tenantId);

        // Act
        var retrievedData = await _sessionService.GetSessionDataAsync<string>(sessionId, dataKey);

        // Assert
        Assert.Null(retrievedData);
    }

    [Fact]
    public async Task DestroySessionAsync_ShouldRemoveSession()
    {
        // Arrange
        var sessionId = "test_session_destroy";
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await _sessionService.CreateSessionAsync(sessionId, userId, tenantId);
        
        // 確認會話存在
        var sessionInfo = await _sessionService.GetSessionAsync(sessionId);
        Assert.NotNull(sessionInfo);

        // Act
        await _sessionService.DestroySessionAsync(sessionId);

        // Assert
        var destroyedSession = await _sessionService.GetSessionAsync(sessionId);
        Assert.Null(destroyedSession);
    }

    [Fact]
    public async Task GetUserSessionsAsync_ShouldReturnUserSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId1 = "user_session_1";
        var sessionId2 = "user_session_2";

        await _sessionService.CreateSessionAsync(sessionId1, userId, tenantId);
        await _sessionService.CreateSessionAsync(sessionId2, userId, tenantId);

        // Act
        var userSessions = await _sessionService.GetUserSessionsAsync(userId);

        // Assert
        var sessionsList = userSessions.ToList();
        Assert.Equal(2, sessionsList.Count);
        Assert.Contains(sessionsList, s => s.SessionId == sessionId1);
        Assert.Contains(sessionsList, s => s.SessionId == sessionId2);
    }

    [Fact]
    public async Task DestroyUserSessionsAsync_ShouldRemoveAllUserSessions_ExceptExcluded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId1 = "user_session_destroy_1";
        var sessionId2 = "user_session_destroy_2";
        var excludeSessionId = "user_session_keep";

        await _sessionService.CreateSessionAsync(sessionId1, userId, tenantId);
        await _sessionService.CreateSessionAsync(sessionId2, userId, tenantId);
        await _sessionService.CreateSessionAsync(excludeSessionId, userId, tenantId);

        // Act
        await _sessionService.DestroyUserSessionsAsync(userId, excludeSessionId);

        // Assert
        var remainingSessions = await _sessionService.GetUserSessionsAsync(userId);
        var remainingSessionsList = remainingSessions.ToList();
        
        Assert.Single(remainingSessionsList);
        Assert.Equal(excludeSessionId, remainingSessionsList.First().SessionId);
    }

    [Fact]
    public async Task SessionInfo_IsExpired_ShouldReturnTrue_WhenExpired()
    {
        // Arrange
        var sessionId = "test_expired_session";
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var shortExpiration = TimeSpan.FromMilliseconds(100);

        await _sessionService.CreateSessionAsync(sessionId, userId, tenantId, null, shortExpiration);

        // 等待會話過期
        await Task.Delay(150);

        // Act
        var sessionInfo = await _sessionService.GetSessionAsync(sessionId);

        // Assert
        // Redis 分散式快取會自動清理過期的會話，所以應該返回 null
        Assert.Null(sessionInfo);
    }

    [Fact]
    public void SessionInfo_IsActive_ShouldReturnTrue_WhenRecentActivity()
    {
        // Arrange
        var sessionInfo = new SessionInfo
        {
            SessionId = "test_active",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5), // 5 分鐘前活動
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        Assert.True(sessionInfo.IsActive);
    }

    [Fact]
    public void SessionInfo_IsActive_ShouldReturnFalse_WhenOldActivity()
    {
        // Arrange
        var sessionInfo = new SessionInfo
        {
            SessionId = "test_inactive",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-45), // 45 分鐘前活動
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        Assert.False(sessionInfo.IsActive);
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_ShouldComplete_WithoutThrowingException()
    {
        // Act & Assert - 應該完成而不拋出異常
        await _sessionService.CleanupExpiredSessionsAsync();
        
        // 驗證日誌呼叫
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("清理過期會話")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}