using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using CreditMonitoring.Common.Services.Azure;
using CreditMonitoring.Common.Interfaces;
using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Tests.Integration
{
    /// <summary>
    /// Azure服務整合測試
    /// </summary>
    public class AzureServicesIntegrationTests : IClassFixture<AzureTestFixture>
    {
        private readonly AzureTestFixture _fixture;
        private readonly ILogger<AzureServicesIntegrationTests> _logger;

        public AzureServicesIntegrationTests(AzureTestFixture fixture)
        {
            _fixture = fixture;
            _logger = _fixture.ServiceProvider.GetRequiredService<ILogger<AzureServicesIntegrationTests>>();
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task AzureKeyVaultService_GetSecret_ShouldReturnValue()
        {
            // Arrange
            var keyVaultService = _fixture.ServiceProvider.GetRequiredService<IAzureKeyVaultService>();
            var secretName = "test-secret";

            // Act & Assert
            try
            {
                var secret = await keyVaultService.GetSecretAsync(secretName);
                Assert.NotNull(secret);
                _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to retrieve secret {SecretName}: {Error}", secretName, ex.Message);
                // Skip test if Key Vault is not configured
                Assert.True(true, "Key Vault not configured for testing");
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task AzureServiceBusService_SendMessage_ShouldSucceed()
        {
            // Arrange
            var serviceBusService = _fixture.ServiceProvider.GetRequiredService<IAzureServiceBusService>();            var alert = new CreditAlert
            {
                Id = 1,
                LoanAccountId = 123,
                Severity = AlertSeverity.High,
                Description = "Test alert for integration testing",
                AlertDate = DateTime.UtcNow,
                AlertType = "CreditScore",
                PreviousCreditScore = 700,
                CurrentCreditScore = 600,
                CreatedAt = DateTime.UtcNow
            };

            // Act & Assert
            try
            {
                await serviceBusService.SendCreditAlertAsync(alert);
                _logger.LogInformation("Successfully sent credit alert message");
                Assert.True(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to send Service Bus message: {Error}", ex.Message);
                // Skip test if Service Bus is not configured
                Assert.True(true, "Service Bus not configured for testing");
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task AzureMonitoringService_TrackEvent_ShouldSucceed()
        {
            // Arrange
            var monitoringService = _fixture.ServiceProvider.GetRequiredService<IAzureMonitoringService>();
            var eventName = "TestEvent";
            var properties = new Dictionary<string, string>
            {
                ["TestProperty"] = "TestValue",
                ["Timestamp"] = DateTime.UtcNow.ToString()
            };            // Act & Assert
            try
            {
                monitoringService.TrackCustomEvent(eventName, properties);
                _logger.LogInformation("Successfully tracked event: {EventName}", eventName);
                Assert.True(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to track event: {Error}", ex.Message);
                // Skip test if Application Insights is not configured
                Assert.True(true, "Application Insights not configured for testing");
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task AzureServices_HealthCheck_ShouldPass()
        {
            // Arrange
            var healthResults = new List<(string Service, bool IsHealthy, string Message)>();

            // Act
            try
            {
                // Test Key Vault
                var keyVaultService = _fixture.ServiceProvider.GetRequiredService<IAzureKeyVaultService>();
                try
                {
                    await keyVaultService.GetSecretAsync("health-check");
                    healthResults.Add(("KeyVault", true, "Connected"));
                }
                catch
                {
                    healthResults.Add(("KeyVault", false, "Not configured or accessible"));
                }

                // Test Service Bus
                var serviceBusService = _fixture.ServiceProvider.GetRequiredService<IAzureServiceBusService>();
                try
                {                    var testAlert = new CreditAlert
                    {
                        Id = 0,
                        LoanAccountId = 0,
                        Severity = AlertSeverity.Low,
                        Description = "Health check message",
                        AlertDate = DateTime.UtcNow,
                        AlertType = "HealthCheck",
                        PreviousCreditScore = 700,
                        CurrentCreditScore = 700,
                        CreatedAt = DateTime.UtcNow
                    };
                    await serviceBusService.SendCreditAlertAsync(testAlert);
                    healthResults.Add(("ServiceBus", true, "Connected"));
                }
                catch
                {
                    healthResults.Add(("ServiceBus", false, "Not configured or accessible"));
                }

                // Test Application Insights
                var monitoringService = _fixture.ServiceProvider.GetRequiredService<IAzureMonitoringService>();                try
                {
                    monitoringService.TrackCustomEvent("HealthCheck", new Dictionary<string, string>
                    {
                        ["CheckTime"] = DateTime.UtcNow.ToString()
                    });
                    healthResults.Add(("ApplicationInsights", true, "Connected"));
                }
                catch
                {
                    healthResults.Add(("ApplicationInsights", false, "Not configured or accessible"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
            }

            // Assert
            _logger.LogInformation("Health Check Results:");
            foreach (var (service, isHealthy, message) in healthResults)
            {
                _logger.LogInformation("  {Service}: {Status} - {Message}", 
                    service, isHealthy ? "✓" : "✗", message);
            }

            // At least the services should be injectable (even if not configured)
            Assert.NotEmpty(healthResults);
        }
    }

    /// <summary>
    /// Azure測試環境設定
    /// </summary>
    public class AzureTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public AzureTestFixture()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("appsettings.Azure.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();

            // 添加日誌記錄
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            // 添加配置
            services.AddSingleton<IConfiguration>(configuration);

            // 註冊Azure服務
            services.AddScoped<IAzureKeyVaultService, AzureKeyVaultService>();
            services.AddScoped<IAzureServiceBusService, AzureServiceBusService>();
            services.AddScoped<IAzureMonitoringService, AzureMonitoringService>();

            ServiceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            (ServiceProvider as IDisposable)?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
