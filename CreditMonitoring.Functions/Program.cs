using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.WorkerService;
using CreditMonitoring.Common.Services.Azure;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        // 添加 Azure Key Vault 配置（如果在 Azure 環境中）
        if (context.HostingEnvironment.IsProduction())
        {
            var keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultUri");
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                config.AddAzureKeyVault(new Uri(keyVaultUri), new Azure.Identity.DefaultAzureCredential());
            }
        }
    })
    .ConfigureServices((context, services) =>
    {
        // 添加 Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // 註冊 Azure 服務
        services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
        services.AddSingleton<IAzureServiceBusService, AzureServiceBusService>();
        services.AddSingleton<IAzureMonitoringService, AzureMonitoringService>();

        // 添加 HTTP 客戶端用於 API 呼叫
        services.AddHttpClient("CreditMonitoringApi", client =>
        {
            var apiBaseUrl = context.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001";
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // 添加日誌配置
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddApplicationInsights();
        });
    })
    .ConfigureLogging((context, logging) =>
    {
        // 設定日誌層級
        logging.SetMinimumLevel(LogLevel.Information);
        
        // 過濾 Azure Functions 內部日誌
        logging.AddFilter("Microsoft.Azure.Functions", LogLevel.Warning);
        logging.AddFilter("Azure.Core", LogLevel.Warning);
        logging.AddFilter("Azure.Identity", LogLevel.Warning);
    })
    .Build();

host.Run();
