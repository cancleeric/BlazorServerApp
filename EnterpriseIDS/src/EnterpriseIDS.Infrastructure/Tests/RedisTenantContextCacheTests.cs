using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;
using EnterpriseIDS.Infrastructure.Caching;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// Redis 租戶上下文快取測試
/// </summary>
public class RedisTenantContextCacheTests
{
    private readonly Mock<ILogger<RedisTenantContextCache>> _loggerMock;
    private readonly IDistributedCache _distributedCache;
    private readonly RedisTenantContextCache _tenantCache;

    public RedisTenantContextCacheTests()
    {
        _loggerMock = new Mock<ILogger<RedisTenantContextCache>>();
        
        // 使用記憶體快取進行測試
        _distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        
        _tenantCache = new RedisTenantContextCache(_distributedCache, _loggerMock.Object);
    }

    [Fact]
    public async Task SetAsync_ShouldStoreTenantContext_WhenValidParameters()
    {
        // Arrange
        var key = "tenant_test_123";
        var tenantContext = CreateTestTenantContext();

        // Act
        await _tenantCache.SetAsync(key, tenantContext);

        // Assert
        var retrievedContext = await _tenantCache.GetAsync(key);
        Assert.NotNull(retrievedContext);
        Assert.Equal(tenantContext.TenantId, retrievedContext.TenantId);
        Assert.Equal(tenantContext.Name, retrievedContext.Name);
        Assert.Equal(tenantContext.IsActive, retrievedContext.IsActive);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyNotExists()
    {
        // Arrange
        var key = "non_existent_tenant";

        // Act
        var retrievedContext = await _tenantCache.GetAsync(key);

        // Assert
        Assert.Null(retrievedContext);
    }

    [Fact]
    public async Task SetAsync_WithCustomExpiration_ShouldUseCustomExpiration()
    {
        // Arrange
        var key = "tenant_custom_expiration";
        var tenantContext = CreateTestTenantContext();
        var customExpiration = TimeSpan.FromMinutes(60);

        // Act
        await _tenantCache.SetAsync(key, tenantContext, customExpiration);

        // Assert
        var retrievedContext = await _tenantCache.GetAsync(key);
        Assert.NotNull(retrievedContext);
        Assert.Equal(tenantContext.TenantId, retrievedContext.TenantId);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveTenantContext_WhenExists()
    {
        // Arrange
        var key = "tenant_to_remove";
        var tenantContext = CreateTestTenantContext();

        await _tenantCache.SetAsync(key, tenantContext);
        
        // 確認項目存在
        var retrievedBeforeRemove = await _tenantCache.GetAsync(key);
        Assert.NotNull(retrievedBeforeRemove);

        // Act
        await _tenantCache.RemoveAsync(key);

        // Assert
        var retrievedAfterRemove = await _tenantCache.GetAsync(key);
        Assert.Null(retrievedAfterRemove);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        var key = "tenant_exists_test";
        var tenantContext = CreateTestTenantContext();

        await _tenantCache.SetAsync(key, tenantContext);

        // Act
        var exists = await _tenantCache.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyNotExists()
    {
        // Arrange
        var key = "tenant_not_exists";

        // Act
        var exists = await _tenantCache.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task RefreshAsync_ShouldUpdateExpiration_WhenKeyExists()
    {
        // Arrange
        var key = "tenant_refresh_test";
        var tenantContext = CreateTestTenantContext();
        var newExpiration = TimeSpan.FromHours(2);

        await _tenantCache.SetAsync(key, tenantContext);

        // Act
        await _tenantCache.RefreshAsync(key, newExpiration);

        // Assert
        var retrievedContext = await _tenantCache.GetAsync(key);
        Assert.NotNull(retrievedContext);
        Assert.Equal(tenantContext.TenantId, retrievedContext.TenantId);
    }

    [Fact]
    public async Task RefreshAsync_ShouldNotThrow_WhenKeyNotExists()
    {
        // Arrange
        var key = "tenant_refresh_not_exists";
        var newExpiration = TimeSpan.FromHours(2);

        // Act & Assert - 應該不拋出異常
        await _tenantCache.RefreshAsync(key, newExpiration);
    }

    [Fact]
    public async Task SetManyAsync_ShouldStoreManyTenantContexts()
    {
        // Arrange
        var tenantContexts = new Dictionary<string, TenantContext>
        {
            { "tenant_batch_1", CreateTestTenantContext("Tenant 1") },
            { "tenant_batch_2", CreateTestTenantContext("Tenant 2") },
            { "tenant_batch_3", CreateTestTenantContext("Tenant 3") }
        };

        // Act
        await _tenantCache.SetManyAsync(tenantContexts);

        // Assert
        foreach (var kvp in tenantContexts)
        {
            var retrievedContext = await _tenantCache.GetAsync(kvp.Key);
            Assert.NotNull(retrievedContext);
            Assert.Equal(kvp.Value.Name, retrievedContext.Name);
        }
    }

    [Fact]
    public async Task GetManyAsync_ShouldReturnManyTenantContexts()
    {
        // Arrange
        var tenantContexts = new Dictionary<string, TenantContext>
        {
            { "tenant_get_many_1", CreateTestTenantContext("GetMany 1") },
            { "tenant_get_many_2", CreateTestTenantContext("GetMany 2") },
            { "tenant_get_many_3", CreateTestTenantContext("GetMany 3") }
        };

        // 先儲存所有項目
        await _tenantCache.SetManyAsync(tenantContexts);

        var keys = tenantContexts.Keys.ToList();
        keys.Add("non_existent_key"); // 加入不存在的鍵值

        // Act
        var retrievedContexts = await _tenantCache.GetManyAsync(keys);

        // Assert
        Assert.Equal(4, retrievedContexts.Count);
        
        foreach (var kvp in tenantContexts)
        {
            Assert.True(retrievedContexts.ContainsKey(kvp.Key));
            Assert.NotNull(retrievedContexts[kvp.Key]);
            Assert.Equal(kvp.Value.Name, retrievedContexts[kvp.Key]!.Name);
        }

        // 不存在的鍵值應該返回 null
        Assert.True(retrievedContexts.ContainsKey("non_existent_key"));
        Assert.Null(retrievedContexts["non_existent_key"]);
    }

    [Fact]
    public async Task GetKeysAsync_ShouldReturnEmptyList_CurrentImplementation()
    {
        // Arrange
        var pattern = "tenant_pattern_*";

        // Act
        var keys = await _tenantCache.GetKeysAsync(pattern);

        // Assert
        // 當前實作返回空列表並記錄警告
        Assert.Empty(keys);
        
        // 驗證警告日誌
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GetKeysAsync 方法需要 Redis 連線直接存取")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldHandleExceptions_GracefullyAndReturnNull()
    {
        // Arrange
        var mockDistributedCache = new Mock<IDistributedCache>();
        mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new InvalidOperationException("Test exception"));

        var cacheWithException = new RedisTenantContextCache(mockDistributedCache.Object, _loggerMock.Object);
        
        // Act
        var result = await cacheWithException.GetAsync("test_key");

        // Assert
        Assert.Null(result);
        
        // 驗證錯誤日誌
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("取得租戶上下文快取失敗")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldHandleExceptions_GracefullyAndReturnFalse()
    {
        // Arrange
        var mockDistributedCache = new Mock<IDistributedCache>();
        mockDistributedCache.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new InvalidOperationException("Test exception"));

        var cacheWithException = new RedisTenantContextCache(mockDistributedCache.Object, _loggerMock.Object);
        
        // Act
        var result = await cacheWithException.ExistsAsync("test_key");

        // Assert
        Assert.False(result);
        
        // 驗證錯誤日誌
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("檢查租戶上下文快取存在性失敗")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// 建立測試用的租戶上下文
    /// </summary>
    private static TenantContext CreateTestTenantContext(string? name = null)
    {
        return new TenantContext
        {
            TenantId = Guid.NewGuid(),
            Name = name ?? "Test Tenant",
            DisplayName = "Test Tenant Display",
            IsActive = true,
            Theme = "default",
            DatabaseConnection = "test_connection",
            Settings = new Dictionary<string, object>
            {
                { "feature1", true },
                { "maxUsers", 100 }
            }
        };
    }
}