using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EnterpriseIDS.Application.Services;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Application.Tests;

/// <summary>
/// 健康檢查服務單元測試
/// </summary>
public class HealthCheckServiceTests
{
    private readonly Mock<ILogger<HealthCheckService>> _mockLogger;
    private readonly Mock<ILdapService> _mockLdapService;
    private readonly Mock<ITenantService> _mockTenantService;
    private readonly HealthCheckService _healthCheckService;

    public HealthCheckServiceTests()
    {
        _mockLogger = new Mock<ILogger<HealthCheckService>>();
        _mockLdapService = new Mock<ILdapService>();
        _mockTenantService = new Mock<ITenantService>();
        
        _healthCheckService = new HealthCheckService(
            _mockLogger.Object,
            _mockLdapService.Object,
            _mockTenantService.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnHealthy_WhenAllComponentsAreHealthy()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _healthCheckService.CheckHealthAsync(cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("綜合健康檢查", result.Name);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CheckReadinessAsync_ShouldReturnHealthy_WhenSystemIsReady()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _healthCheckService.CheckReadinessAsync(cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("系統就緒檢查", result.Name);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CheckLivenessAsync_ShouldReturnHealthy_WhenSystemIsAlive()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _healthCheckService.CheckLivenessAsync(cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("系統存活檢查", result.Name);
        Assert.Equal("系統正常運行", result.Description);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetHealthSummaryAsync_ShouldReturnDetailedSummary()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _healthCheckService.GetHealthSummaryAsync(cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Components.Count > 0);
        Assert.NotNull(result.SystemInfo);
        Assert.True(result.TotalCheckTime > TimeSpan.Zero);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("database")]
    [InlineData("tenant")]
    [InlineData("ldap")]
    public async Task CheckComponentHealthAsync_ShouldReturnResult_ForValidComponents(string componentName)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _healthCheckService.CheckComponentHealthAsync(componentName, cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(componentName, result.Name, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckComponentHealthAsync_ShouldReturnUnhealthy_ForUnknownComponent()
    {
        // Arrange
        var componentName = "unknown-component";
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _healthCheckService.CheckComponentHealthAsync(componentName, cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(componentName, result.Name);
        Assert.Equal("未知的組件名稱", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // 已取消的令牌

        // Act
        var result = await _healthCheckService.CheckHealthAsync(cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }
}