using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EnterpriseIDS.Presentation.Controllers;
using EnterpriseIDS.Application.Services;

namespace EnterpriseIDS.Presentation.Tests;

/// <summary>
/// 指標控制器單元測試
/// </summary>
public class MetricsControllerTests
{
    private readonly Mock<ILogger<MetricsController>> _mockLogger;
    private readonly Mock<MetricsCollectionService> _mockMetricsService;
    private readonly MetricsController _controller;

    public MetricsControllerTests()
    {
        _mockLogger = new Mock<ILogger<MetricsController>>();
        _mockMetricsService = new Mock<MetricsCollectionService>(Mock.Of<ILogger<MetricsCollectionService>>());
        _controller = new MetricsController(_mockLogger.Object, _mockMetricsService.Object);
    }

    [Fact]
    public void GetMetricsSummary_ShouldReturnOk_WithValidSummary()
    {
        // Arrange
        var expectedSummary = new Dictionary<string, object>
        {
            { "SystemMetrics", "test" },
            { "ApplicationMetrics", "test" }
        };

        _mockMetricsService
            .Setup(x => x.GetMetricsSummary())
            .Returns(expectedSummary);

        // Act
        var result = _controller.GetMetricsSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSummary = Assert.IsType<Dictionary<string, object>>(okResult.Value);
        Assert.Equal(expectedSummary.Count, returnedSummary.Count);
    }

    [Fact]
    public void GetMetricsSummary_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockMetricsService
            .Setup(x => x.GetMetricsSummary())
            .Throws(new Exception("測試異常"));

        // Act
        var result = _controller.GetMetricsSummary();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public void UpdateSystemMetrics_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        _mockMetricsService
            .Setup(x => x.UpdateSystemMetrics())
            .Verifiable();

        // Act
        var result = _controller.UpdateSystemMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        _mockMetricsService.Verify(x => x.UpdateSystemMetrics(), Times.Once);
    }

    [Fact]
    public void UpdateSystemMetrics_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockMetricsService
            .Setup(x => x.UpdateSystemMetrics())
            .Throws(new Exception("測試異常"));

        // Act
        var result = _controller.UpdateSystemMetrics();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public void GetSystemStatus_ShouldReturnOk_WithSystemInfo()
    {
        // Act
        var result = _controller.GetSystemStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        
        // 檢查回應包含系統資訊
        var response = okResult.Value;
        var responseType = response.GetType();
        Assert.NotNull(responseType.GetProperty("Server"));
        Assert.NotNull(responseType.GetProperty("Process"));
        Assert.NotNull(responseType.GetProperty("Memory"));
        Assert.NotNull(responseType.GetProperty("Environment"));
        Assert.NotNull(responseType.GetProperty("Timestamp"));
    }

    [Fact]
    public void RecordTestMetrics_ShouldReturnOk_WithValidRequest()
    {
        // Arrange
        var testRequest = new TestMetricsRequest
        {
            AuthResult = "success",
            TokenType = "access_token",
            TenantId = "test-tenant",
            ActiveSessions = 42,
            ActiveConnections = 15
        };

        // Act
        var result = _controller.RecordTestMetrics(testRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        
        // 驗證指標記錄方法被呼叫
        _mockMetricsService.Verify(x => x.RecordAuthenticationAttempt("success", "test-tenant"), Times.Once);
        _mockMetricsService.Verify(x => x.RecordTokenIssued("access_token", "test-tenant"), Times.Once);
        _mockMetricsService.Verify(x => x.SetActiveSessions(42, "test-tenant"), Times.Once);
        _mockMetricsService.Verify(x => x.SetActiveConnections(15), Times.Once);
    }

    [Fact]
    public void RecordTestMetrics_ShouldReturnBadRequest_WithNullRequest()
    {
        // Act
        var result = _controller.RecordTestMetrics(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("請提供有效的測試指標請求", badRequestResult.Value?.ToString() ?? "");
    }

    [Fact]
    public void RecordTestMetrics_ShouldHandleLdapMetrics_WhenProvided()
    {
        // Arrange
        var testRequest = new TestMetricsRequest
        {
            LdapOperation = "bind",
            LdapResult = "success",
            DurationMs = 100
        };

        // Act
        var result = _controller.RecordTestMetrics(testRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        
        // 驗證 LDAP 指標記錄方法被呼叫
        _mockMetricsService.Verify(x => x.RecordLdapOperation("bind", "success", It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public void RecordTestMetrics_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        var testRequest = new TestMetricsRequest
        {
            AuthResult = "success",
            TenantId = "test-tenant"
        };

        _mockMetricsService
            .Setup(x => x.RecordAuthenticationAttempt(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new Exception("測試異常"));

        // Act
        var result = _controller.RecordTestMetrics(testRequest);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("  ", "  ", "  ")]
    public void RecordTestMetrics_ShouldSkipEmptyValues(string authResult, string tokenType, string tenantId)
    {
        // Arrange
        var testRequest = new TestMetricsRequest
        {
            AuthResult = authResult,
            TokenType = tokenType,
            TenantId = tenantId
        };

        // Act
        var result = _controller.RecordTestMetrics(testRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
        
        // 驗證空值不會觸發指標記錄
        _mockMetricsService.Verify(x => x.RecordAuthenticationAttempt(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockMetricsService.Verify(x => x.RecordTokenIssued(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}