using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EnterpriseIDS.Presentation;
using EnterpriseIDS.Core.ValueObjects;
using EnterpriseIDS.Application.Services;

namespace EnterpriseIDS.IntegrationTests;

/// <summary>
/// 監控系統整合測試
/// </summary>
public class MonitoringIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MonitoringIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthCheckResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        healthResponse.Should().NotBeNull();
        healthResponse.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
        healthResponse.Name.Should().NotBeNullOrEmpty();
        healthResponse.CheckTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task HealthReadyEndpoint_ShouldReturnReadyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthCheckResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        healthResponse.Should().NotBeNull();
        healthResponse.Name.Should().Contain("就緒");
    }

    [Fact]
    public async Task HealthLiveEndpoint_ShouldReturnLiveStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthCheckResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        healthResponse.Should().NotBeNull();
        healthResponse.Name.Should().Contain("存活");
    }

    [Fact]
    public async Task HealthSummaryEndpoint_ShouldReturnDetailedSummary()
    {
        // Act
        var response = await _client.GetAsync("/health/summary");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthSummary = JsonSerializer.Deserialize<HealthCheckSummary>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        healthSummary.Should().NotBeNull();
        healthSummary.Components.Should().NotBeEmpty();
        healthSummary.SystemInfo.Should().NotBeNull();
        healthSummary.ServiceVersion.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("application")]
    [InlineData("database")]
    [InlineData("tenant")]
    [InlineData("ldap")]
    public async Task HealthComponentEndpoint_ShouldReturnComponentStatus(string component)
    {
        // Act
        var response = await _client.GetAsync($"/health/component/{component}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthCheckResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        healthResponse.Should().NotBeNull();
        healthResponse.Name.Should().Be(component, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldReturnPrometheusFormat()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Contain("text/plain");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // 檢查包含 Enterprise IDS 特定指標
        content.Should().Contain("enterprise_ids_");
        content.Should().Contain("# HELP");
        content.Should().Contain("# TYPE");
    }

    [Fact]
    public async Task MetricsSummaryEndpoint_ShouldReturnMetricsSummary()
    {
        // Act
        var response = await _client.GetAsync("/api/metrics/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var summary = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        summary.Should().NotBeNull();
        summary.Should().ContainKeys("SystemMetrics", "ApplicationMetrics", "BusinessMetrics", "LdapMetrics", "HealthCheckMetrics");
    }

    [Fact]
    public async Task MetricsUpdateEndpoint_ShouldUpdateSystemMetrics()
    {
        // Act
        var response = await _client.PostAsync("/api/metrics/update-system", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("系統指標已更新");
    }

    [Fact]
    public async Task SystemStatusEndpoint_ShouldReturnSystemInformation()
    {
        // Act
        var response = await _client.GetAsync("/api/metrics/system-status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var systemStatus = JsonSerializer.Deserialize<JsonElement>(content);

        systemStatus.TryGetProperty("Server", out _).Should().BeTrue();
        systemStatus.TryGetProperty("Process", out _).Should().BeTrue();
        systemStatus.TryGetProperty("Memory", out _).Should().BeTrue();
        systemStatus.TryGetProperty("Environment", out _).Should().BeTrue();
        systemStatus.TryGetProperty("Timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TestMetricsEndpoint_ShouldRecordTestMetrics()
    {
        // Arrange
        var testMetrics = new
        {
            AuthResult = "success",
            TokenType = "access_token",
            TenantId = "test-tenant",
            ActiveSessions = 42,
            ActiveConnections = 15,
            LdapOperation = "bind",
            LdapResult = "success",
            DurationMs = 100
        };

        var json = JsonSerializer.Serialize(testMetrics);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/metrics/test", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("測試指標已記錄");
    }

    [Fact]
    public async Task PingEndpoint_ShouldReturnPong()
    {
        // Act
        var response = await _client.GetAsync("/health/ping");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("pong");
    }

    [Fact]
    public async Task VersionEndpoint_ShouldReturnVersionInfo()
    {
        // Act
        var response = await _client.GetAsync("/health/version");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var versionInfo = JsonSerializer.Deserialize<JsonElement>(content);

        versionInfo.TryGetProperty("ServiceName", out _).Should().BeTrue();
        versionInfo.TryGetProperty("Version", out _).Should().BeTrue();
        versionInfo.TryGetProperty("Environment", out _).Should().BeTrue();
        versionInfo.TryGetProperty("Framework", out _).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleHealthChecks_ShouldMaintainConsistentStatus()
    {
        // Act - 執行多次健康檢查
        var healthTasks = Enumerable.Range(0, 5)
            .Select(_ => _client.GetAsync("/health"))
            .ToArray();

        var responses = await Task.WhenAll(healthTasks);

        // Assert - 所有回應應該成功
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        }

        // 狀態應該保持一致
        var contents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        var healthStatuses = contents.Select(content =>
        {
            var health = JsonSerializer.Deserialize<HealthCheckResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return health?.Status;
        }).ToList();

        // 所有狀態應該相同（在短時間內）
        healthStatuses.Should().AllBeEquivalentTo(healthStatuses.First());
    }

    [Fact]
    public async Task MetricsCollection_ShouldBeThreadSafe()
    {
        // Arrange
        var testMetrics = new
        {
            AuthResult = "success",
            TokenType = "access_token",
            TenantId = "load-test-tenant",
            ActiveSessions = 1,
            ActiveConnections = 1
        };

        var json = JsonSerializer.Serialize(testMetrics);

        // Act - 並行發送多個請求
        var metricsTasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsync("/api/metrics/test", 
                new StringContent(json, Encoding.UTF8, "application/json")))
            .ToArray();

        var responses = await Task.WhenAll(metricsTasks);

        // Assert - 所有請求應該成功
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task HealthCheckPerformance_ShouldMeetResponseTimeRequirements()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/health");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Health check should respond within 1 second");
    }

    [Fact]
    public async Task MetricsEndpointPerformance_ShouldMeetResponseTimeRequirements()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/metrics");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Metrics endpoint should respond within 2 seconds");
    }
}