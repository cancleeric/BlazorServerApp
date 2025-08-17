using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EnterpriseIDS.Presentation.Controllers;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Presentation.Tests;

/// <summary>
/// 健康檢查控制器單元測試
/// </summary>
public class HealthControllerTests
{
    private readonly Mock<ILogger<HealthController>> _mockLogger;
    private readonly Mock<IHealthCheckService> _mockHealthCheckService;
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _mockLogger = new Mock<ILogger<HealthController>>();
        _mockHealthCheckService = new Mock<IHealthCheckService>();
        _controller = new HealthController(_mockLogger.Object, _mockHealthCheckService.Object);
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOk_WhenHealthy()
    {
        // Arrange
        var healthResponse = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Name = "測試健康檢查",
            Description = "系統正常",
            CheckTime = DateTime.UtcNow
        };

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResponse);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedHealth = Assert.IsType<HealthCheckResponse>(okResult.Value);
        Assert.Equal(HealthStatus.Healthy, returnedHealth.Status);
    }

    [Fact]
    public async Task GetHealth_ShouldReturnDegraded_WhenDegraded()
    {
        // Arrange
        var healthResponse = new HealthCheckResponse
        {
            Status = HealthStatus.Degraded,
            Name = "測試健康檢查",
            Description = "系統降級",
            CheckTime = DateTime.UtcNow
        };

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResponse);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(200, statusResult.StatusCode);
        var returnedHealth = Assert.IsType<HealthCheckResponse>(statusResult.Value);
        Assert.Equal(HealthStatus.Degraded, returnedHealth.Status);
    }

    [Fact]
    public async Task GetHealth_ShouldReturnServiceUnavailable_WhenUnhealthy()
    {
        // Arrange
        var healthResponse = new HealthCheckResponse
        {
            Status = HealthStatus.Unhealthy,
            Name = "測試健康檢查",
            Description = "系統不健康",
            CheckTime = DateTime.UtcNow
        };

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResponse);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, statusResult.StatusCode);
        var returnedHealth = Assert.IsType<HealthCheckResponse>(statusResult.Value);
        Assert.Equal(HealthStatus.Unhealthy, returnedHealth.Status);
    }

    [Fact]
    public async Task GetHealth_ShouldReturnServiceUnavailable_WhenExceptionThrown()
    {
        // Arrange
        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("測試異常"));

        // Act
        var result = await _controller.GetHealth();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, statusResult.StatusCode);
        var returnedHealth = Assert.IsType<HealthCheckResponse>(statusResult.Value);
        Assert.Equal(HealthStatus.Unhealthy, returnedHealth.Status);
        Assert.Contains("測試異常", returnedHealth.ErrorMessage);
    }

    [Fact]
    public async Task GetHealthSummary_ShouldReturnOk_WhenHealthy()
    {
        // Arrange
        var healthSummary = new HealthCheckSummary
        {
            OverallStatus = HealthStatus.Healthy,
            CheckTime = DateTime.UtcNow,
            ServiceVersion = "1.0.0"
        };

        _mockHealthCheckService
            .Setup(x => x.GetHealthSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthSummary);

        // Act
        var result = await _controller.GetHealthSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSummary = Assert.IsType<HealthCheckSummary>(okResult.Value);
        Assert.Equal(HealthStatus.Healthy, returnedSummary.OverallStatus);
    }

    [Fact]
    public async Task GetReadiness_ShouldReturnOk_WhenReady()
    {
        // Arrange
        var readinessResponse = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Name = "就緒檢查",
            Description = "系統已就緒",
            CheckTime = DateTime.UtcNow
        };

        _mockHealthCheckService
            .Setup(x => x.CheckReadinessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(readinessResponse);

        // Act
        var result = await _controller.GetReadiness();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedReadiness = Assert.IsType<HealthCheckResponse>(okResult.Value);
        Assert.Equal(HealthStatus.Healthy, returnedReadiness.Status);
    }

    [Fact]
    public async Task GetLiveness_ShouldReturnOk_WhenAlive()
    {
        // Arrange
        var livenessResponse = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Name = "存活檢查",
            Description = "系統存活",
            CheckTime = DateTime.UtcNow
        };

        _mockHealthCheckService
            .Setup(x => x.CheckLivenessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(livenessResponse);

        // Act
        var result = await _controller.GetLiveness();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedLiveness = Assert.IsType<HealthCheckResponse>(okResult.Value);
        Assert.Equal(HealthStatus.Healthy, returnedLiveness.Status);
    }

    [Fact]
    public async Task GetComponentHealth_ShouldReturnOk_WhenComponentHealthy()
    {
        // Arrange
        var componentName = "database";
        var componentResponse = new HealthCheckResponse
        {
            Status = HealthStatus.Healthy,
            Name = componentName,
            Description = "資料庫正常",
            CheckTime = DateTime.UtcNow
        };

        _mockHealthCheckService
            .Setup(x => x.CheckComponentHealthAsync(componentName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(componentResponse);

        // Act
        var result = await _controller.GetComponentHealth(componentName);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedComponent = Assert.IsType<HealthCheckResponse>(okResult.Value);
        Assert.Equal(HealthStatus.Healthy, returnedComponent.Status);
        Assert.Equal(componentName, returnedComponent.Name);
    }

    [Fact]
    public void Ping_ShouldReturnOk_WithValidResponse()
    {
        // Act
        var result = _controller.Ping();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        
        // 檢查回應包含必要欄位
        var response = okResult.Value;
        var responseType = response.GetType();
        Assert.NotNull(responseType.GetProperty("message"));
        Assert.NotNull(responseType.GetProperty("timestamp"));
        Assert.NotNull(responseType.GetProperty("server"));
        Assert.NotNull(responseType.GetProperty("version"));
    }

    [Fact]
    public void GetVersion_ShouldReturnOk_WithVersionInfo()
    {
        // Act
        var result = _controller.GetVersion();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        
        // 檢查回應包含版本資訊
        var response = okResult.Value;
        var responseType = response.GetType();
        Assert.NotNull(responseType.GetProperty("ServiceName"));
        Assert.NotNull(responseType.GetProperty("Version"));
        Assert.NotNull(responseType.GetProperty("Environment"));
    }
}