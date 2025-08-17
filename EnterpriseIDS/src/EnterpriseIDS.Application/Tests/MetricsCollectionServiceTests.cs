using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EnterpriseIDS.Application.Services;

namespace EnterpriseIDS.Application.Tests;

/// <summary>
/// 指標收集服務單元測試
/// </summary>
public class MetricsCollectionServiceTests
{
    private readonly Mock<ILogger<MetricsCollectionService>> _mockLogger;
    private readonly MetricsCollectionService _metricsService;

    public MetricsCollectionServiceTests()
    {
        _mockLogger = new Mock<ILogger<MetricsCollectionService>>();
        _metricsService = new MetricsCollectionService(_mockLogger.Object);
    }

    [Fact]
    public void UpdateSystemMetrics_ShouldExecuteWithoutException()
    {
        // Act & Assert
        var exception = Record.Exception(() => _metricsService.UpdateSystemMetrics());
        Assert.Null(exception);
    }

    [Fact]
    public void RecordHttpRequest_ShouldExecuteWithoutException()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/health";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordHttpRequest(method, endpoint, statusCode, duration));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordAuthenticationAttempt_ShouldExecuteWithoutException()
    {
        // Arrange
        var result = "success";
        var tenantId = "test-tenant";

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordAuthenticationAttempt(result, tenantId));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordTokenIssued_ShouldExecuteWithoutException()
    {
        // Arrange
        var tokenType = "access_token";
        var tenantId = "test-tenant";

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordTokenIssued(tokenType, tenantId));
        Assert.Null(exception);
    }

    [Fact]
    public void SetActiveSessions_ShouldExecuteWithoutException()
    {
        // Arrange
        var count = 42;
        var tenantId = "test-tenant";

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.SetActiveSessions(count, tenantId));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordTenantOperation_ShouldExecuteWithoutException()
    {
        // Arrange
        var operation = "create";
        var tenantId = "test-tenant";

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordTenantOperation(operation, tenantId));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordLdapOperation_ShouldExecuteWithoutException()
    {
        // Arrange
        var operation = "bind";
        var result = "success";
        var duration = TimeSpan.FromMilliseconds(50);

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordLdapOperation(operation, result, duration));
        Assert.Null(exception);
    }

    [Fact]
    public void SetLdapActiveConnections_ShouldExecuteWithoutException()
    {
        // Arrange
        var count = 5;

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.SetLdapActiveConnections(count));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordHealthCheck_ShouldExecuteWithoutException()
    {
        // Arrange
        var component = "database";
        var status = 2; // Healthy
        var duration = TimeSpan.FromMilliseconds(30);

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordHealthCheck(component, status, duration));
        Assert.Null(exception);
    }

    [Fact]
    public void SetActiveConnections_ShouldExecuteWithoutException()
    {
        // Arrange
        var count = 10;

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.SetActiveConnections(count));
        Assert.Null(exception);
    }

    [Fact]
    public void GetMetricsSummary_ShouldReturnValidDictionary()
    {
        // Act
        var result = _metricsService.GetMetricsSummary();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        Assert.Contains("SystemMetrics", result.Keys);
        Assert.Contains("ApplicationMetrics", result.Keys);
        Assert.Contains("BusinessMetrics", result.Keys);
        Assert.Contains("LdapMetrics", result.Keys);
        Assert.Contains("HealthCheckMetrics", result.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public void RecordAuthenticationAttempt_ShouldHandleEmptyTenantId(string tenantId)
    {
        // Arrange
        var result = "success";

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.RecordAuthenticationAttempt(result, tenantId));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1000)]
    public void SetActiveSessions_ShouldHandleVariousCounts(int count)
    {
        // Arrange
        var tenantId = "test-tenant";

        // Act & Assert
        var exception = Record.Exception(() => 
            _metricsService.SetActiveSessions(count, tenantId));
        Assert.Null(exception);
    }
}