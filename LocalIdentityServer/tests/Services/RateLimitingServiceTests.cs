using LocalIdentityServer.Models;
using LocalIdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace LocalIdentityServer.Tests.Services;

/// <summary>
/// RateLimitingService 單元測試
/// 驗證多層級限流、Sliding Window 演算法與安全防護機制
/// </summary>
public class RateLimitingServiceTests
{
    private readonly Mock<IRateLimitingCacheService> _mockCache;
    private readonly Mock<ILogger<RateLimitingService>> _mockLogger;
    private readonly RateLimitingOptions _options;
    private readonly RateLimitingService _service;

    public RateLimitingServiceTests()
    {
        _mockCache = new Mock<IRateLimitingCacheService>();
        _mockLogger = new Mock<ILogger<RateLimitingService>>();
        
        _options = new RateLimitingOptions
        {
            Enabled = true,
            DefaultWindowSizeSeconds = 60,
            WindowSegments = 10,
            Rules = new List<RateLimitRule>
            {
                new RateLimitRule
                {
                    Name = "test_api",
                    EndpointPattern = "/api/test",
                    Priority = 10,
                    Enabled = true,
                    Limits = new RateLimitLevels
                    {
                        IpLevel = new RateLimitConfig { MaxRequests = 10, WindowSizeSeconds = 60, Enabled = true },
                        UserLevel = new RateLimitConfig { MaxRequests = 5, WindowSizeSeconds = 60, Enabled = true },
                        EndpointLevel = new RateLimitConfig { MaxRequests = 100, WindowSizeSeconds = 60, Enabled = true }
                    }
                }
            }
        };

        var optionsWrapper = Options.Create(_options);
        _service = new RateLimitingService(_mockCache.Object, optionsWrapper, _mockLogger.Object);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenDisabled_ShouldAllowAllRequests()
    {
        // Arrange
        _options.Enabled = false;
        var identifier = CreateTestIdentifier();

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(int.MaxValue, result.Limit);
        Assert.Equal(int.MaxValue, result.Remaining);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WithinIpLimit_ShouldAllowRequest()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        
        _mockCache.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false); // 不在黑白名單
        
        _mockCache.Setup(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(5); // 在限制範圍內

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(10, result.Limit); // IP 層級限制
        Assert.Equal(5, result.Remaining); // 10 - 5 = 5
    }

    [Fact]
    public async Task CheckRateLimitAsync_ExceedsIpLimit_ShouldDenyRequest()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        
        _mockCache.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false); // 不在黑白名單
        
        _mockCache.Setup(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(15); // 超過限制

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal("test_api", result.RuleName);
        Assert.Equal(RateLimitLevel.IpLevel, result.TriggeredLevel);
        Assert.Equal(10, result.Limit);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task CheckRateLimitAsync_AuthenticatedUserExceedsUserLimit_ShouldDenyRequest()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        identifier.UserId = "user123";
        
        _mockCache.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        
        // IP 層級通過，但使用者層級超過
        _mockCache.Setup(x => x.IncrementAsync(It.Is<string>(key => key.Contains("ip:")), It.IsAny<TimeSpan>()))
            .ReturnsAsync(5); // IP 層級 OK
        
        _mockCache.Setup(x => x.IncrementAsync(It.Is<string>(key => key.Contains("user:")), It.IsAny<TimeSpan>()))
            .ReturnsAsync(8); // 使用者層級超過 (限制為 5)

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(RateLimitLevel.UserLevel, result.TriggeredLevel);
        Assert.Equal(5, result.Limit); // 使用者層級限制
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhitelistedIp_ShouldAlwaysAllow()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        
        _mockCache.Setup(x => x.ExistsAsync(It.Is<string>(key => key.Contains("whitelist"))))
            .ReturnsAsync(true); // 在白名單中

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(int.MaxValue, result.Limit);
        Assert.Equal(int.MaxValue, result.Remaining);
    }

    [Fact]
    public async Task CheckRateLimitAsync_BlacklistedIp_ShouldAlwaysDeny()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        
        _mockCache.Setup(x => x.ExistsAsync(It.Is<string>(key => key.Contains("whitelist"))))
            .ReturnsAsync(false); // 不在白名單
        
        _mockCache.Setup(x => x.ExistsAsync(It.Is<string>(key => key.Contains("blacklist"))))
            .ReturnsAsync(true); // 在黑名單中

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(RateLimitLevel.IpLevel, result.TriggeredLevel);
        Assert.Equal("blacklist", result.RuleName);
    }

    [Fact]
    public async Task CheckRateLimitAsync_NoMatchingRule_ShouldAllowRequest()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        identifier.EndpointPath = "/api/unmatched"; // 不匹配任何規則
        
        _mockCache.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(int.MaxValue, result.Limit);
    }

    [Fact]
    public async Task CheckRateLimitBatchAsync_ShouldProcessMultipleRequests()
    {
        // Arrange
        var identifiers = new List<RateLimitIdentifier>
        {
            CreateTestIdentifier("192.168.1.1"),
            CreateTestIdentifier("192.168.1.2"),
            CreateTestIdentifier("192.168.1.3")
        };
        
        _mockCache.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        
        _mockCache.Setup(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(1);

        // Act
        var results = await _service.CheckRateLimitBatchAsync(identifiers);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.IsAllowed));
    }

    [Fact]
    public async Task GetCurrentStatusAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        identifier.UserId = "user123";
        
        var countsDict = new Dictionary<string, long>
        {
            ["ip:test_api:192.168.1.1"] = 3,
            ["user:test_api:user123"] = 2,
            ["endpoint:test_api:/api/test"] = 5
        };
        
        _mockCache.Setup(x => x.ExistsAsync(It.Is<string>(key => key.Contains("whitelist"))))
            .ReturnsAsync(false);
        
        _mockCache.Setup(x => x.ExistsAsync(It.Is<string>(key => key.Contains("blacklist"))))
            .ReturnsAsync(false);
        
        _mockCache.Setup(x => x.GetCountBatchAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(countsDict);

        // Act
        var status = await _service.GetCurrentStatusAsync(identifier);

        // Assert
        Assert.False(status.IsWhitelisted);
        Assert.False(status.IsBlacklisted);
        Assert.Equal(3, status.CurrentCounts[RateLimitLevel.IpLevel]);
        Assert.Equal(2, status.CurrentCounts[RateLimitLevel.UserLevel]);
        Assert.Equal(5, status.CurrentCounts[RateLimitLevel.EndpointLevel]);
        Assert.Equal(10, status.Limits[RateLimitLevel.IpLevel]);
        Assert.Equal(5, status.Limits[RateLimitLevel.UserLevel]);
        Assert.Equal(100, status.Limits[RateLimitLevel.EndpointLevel]);
    }

    [Fact]
    public async Task ResetCounterAsync_ShouldDeleteAllCounters()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        identifier.UserId = "user123";

        // Act
        await _service.ResetCounterAsync(identifier, "Test reset");

        // Assert
        _mockCache.Verify(x => x.DeleteCountAsync(It.Is<string>(key => key.Contains("ip:"))), Times.Once);
        _mockCache.Verify(x => x.DeleteCountAsync(It.Is<string>(key => key.Contains("user:"))), Times.Once);
        _mockCache.Verify(x => x.DeleteCountAsync(It.Is<string>(key => key.Contains("endpoint:"))), Times.Once);
    }

    [Fact]
    public async Task BlacklistTemporaryAsync_ShouldSetBlacklistKey()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        var durationMinutes = 30;
        var reason = "Too many violations";

        // Act
        await _service.BlacklistTemporaryAsync(identifier, durationMinutes, reason);

        // Assert
        _mockCache.Verify(x => x.SetCountAsync(
            It.Is<string>(key => key.Contains("blacklist")),
            1,
            TimeSpan.FromMinutes(durationMinutes)), Times.Once);
    }

    [Fact]
    public async Task WhitelistAsync_ShouldSetWhitelistKey()
    {
        // Arrange
        var identifier = CreateTestIdentifier();
        var reason = "Trusted IP";

        // Act
        await _service.WhitelistAsync(identifier, reason);

        // Assert
        _mockCache.Verify(x => x.SetCountAsync(
            It.Is<string>(key => key.Contains("whitelist")),
            1,
            TimeSpan.FromDays(365)), Times.Once);
    }

    [Fact]
    public async Task AddOrUpdateRuleAsync_ShouldAddNewRule()
    {
        // Arrange
        var newRule = new RateLimitRule
        {
            Name = "new_rule",
            EndpointPattern = "/api/new",
            Priority = 20,
            Enabled = true,
            Limits = new RateLimitLevels
            {
                IpLevel = new RateLimitConfig { MaxRequests = 20, WindowSizeSeconds = 60, Enabled = true }
            }
        };

        // Act
        await _service.AddOrUpdateRuleAsync(newRule);
        var activeRules = await _service.GetActiveRulesAsync();

        // Assert
        Assert.Contains(activeRules, r => r.Name == "new_rule");
        Assert.True(activeRules.Count >= 2); // 原有 + 新增
    }

    [Fact]
    public async Task RemoveRuleAsync_ShouldRemoveExistingRule()
    {
        // Arrange
        var ruleName = "test_api";

        // Act
        await _service.RemoveRuleAsync(ruleName);
        var activeRules = await _service.GetActiveRulesAsync();

        // Assert
        Assert.DoesNotContain(activeRules, r => r.Name == ruleName);
    }

    [Theory]
    [InlineData("/api/test", "/api/test", true)]
    [InlineData("/api/test", "/api/*", true)]
    [InlineData("/api/test", "/api/tes?", true)]
    [InlineData("/api/test", "/api/other", false)]
    [InlineData("/api/v1/test", "/api/*/test", true)]
    public async Task CheckRateLimitAsync_PatternMatching_ShouldWorkCorrectly(
        string requestPath, string pattern, bool shouldMatch)
    {
        // Arrange
        _options.Rules.Clear();
        _options.Rules.Add(new RateLimitRule
        {
            Name = "pattern_test",
            EndpointPattern = pattern,
            Priority = 10,
            Enabled = true,
            Limits = new RateLimitLevels
            {
                IpLevel = new RateLimitConfig { MaxRequests = 1, WindowSizeSeconds = 60, Enabled = true }
            }
        });

        var identifier = CreateTestIdentifier();
        identifier.EndpointPath = requestPath;
        
        _mockCache.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        
        _mockCache.Setup(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(2); // 超過限制

        // Act
        var result = await _service.CheckRateLimitAsync(identifier);

        // Assert
        if (shouldMatch)
        {
            Assert.False(result.IsAllowed); // 規則匹配且超過限制
            Assert.Equal("pattern_test", result.RuleName);
        }
        else
        {
            Assert.True(result.IsAllowed); // 規則不匹配，允許通過
        }
    }

    [Fact]
    public async Task RecordRequestAsync_ShouldIncrementStatistics()
    {
        // Arrange
        var identifier = CreateTestIdentifier();

        // Act
        await _service.RecordRequestAsync(identifier, true);
        await _service.RecordRequestAsync(identifier, false);

        // Assert
        _mockCache.Verify(x => x.IncrementAsync(
            It.Is<string>(key => key.Contains("stats:allowed")),
            TimeSpan.FromHours(24)), Times.Once);
        
        _mockCache.Verify(x => x.IncrementAsync(
            It.Is<string>(key => key.Contains("stats:denied")),
            TimeSpan.FromHours(24)), Times.Once);
    }

    private RateLimitIdentifier CreateTestIdentifier(string ipAddress = "192.168.1.1")
    {
        return new RateLimitIdentifier
        {
            IpAddress = ipAddress,
            EndpointPath = "/api/test",
            HttpMethod = "GET",
            UserAgent = "TestAgent/1.0",
            RequestTime = DateTime.UtcNow
        };
    }
}