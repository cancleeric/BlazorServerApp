using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using EnterpriseIDS.Presentation.Extensions;
using EnterpriseIDS.Presentation.Middleware;
using EnterpriseIDS.Application.Services;
using EnterpriseIDS.Infrastructure.Extensions;
using EnterpriseIDS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog 日誌
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/enterprise-ids-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
    {
        IndexFormat = "enterprise-ids-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true,
        OverwriteTemplate = true,
        DetectElasticsearchVersion = true,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
        NumberOfReplicas = 1,
        NumberOfShards = 2
    })
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 註冊 Infrastructure 服務 (包含 JWT Token 服務和資料庫)
builder.Services.AddInfrastructure(builder.Configuration);

// 註冊監控服務
builder.Services.AddMonitoringServices();
builder.Services.AddPrometheusMetrics();

// 註冊 HttpClient 用於外部服務監控
builder.Services.AddHttpClient();

// 添加健康檢查（包含資料庫檢查）
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EnterpriseIdentityDbContext>("database");

var app = builder.Build();

// 初始化資料庫
await InitializeDatabaseAsync(app);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 使用 Prometheus 指標中介軟體
app.UseHttpMetrics(); // Prometheus 內建的 HTTP 指標

// 使用自訂 HTTP 指標中介軟體
app.UseMiddleware<HttpMetricsMiddleware>();

// 啟用 Prometheus 指標端點
app.UseMetricServer(); // 在 /metrics 端點提供指標

// 認證和授權中介軟體（必須在正確順序）
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 對應健康檢查端點
app.MapHealthChecks("/health");

// 啟動時初始化指標服務
var metricsService = app.Services.GetRequiredService<MetricsCollectionService>();

// 設定背景任務定期更新系統指標
var cancellationTokenSource = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    while (!cancellationTokenSource.Token.IsCancellationRequested)
    {
        try
        {
            metricsService.UpdateSystemMetrics();
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新系統指標時發生錯誤");
        }
    }
}, cancellationTokenSource.Token);

// 設定應用程式關閉時的清理
app.Lifetime.ApplicationStopping.Register(() =>
{
    cancellationTokenSource.Cancel();
    Log.CloseAndFlush();
});

Log.Information("Enterprise Identity Server 正在啟動...");
Log.Information("Prometheus 指標端點: /metrics");
Log.Information("健康檢查端點: /health");
Log.Information("API 文檔: /swagger");
Log.Information("JWT Token 服務已註冊並啟用");

app.Run();

// 初始化資料庫
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<EnterpriseIdentityDbContext>();
    
    try
    {
        Log.Information("正在檢查資料庫狀態...");
        
        // 確保資料庫已創建
        await context.Database.EnsureCreatedAsync();
        
        // 執行任何待處理的 Migration
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            Log.Information("發現待處理的 Migration: {Migrations}", string.Join(", ", pendingMigrations));
            await context.Database.MigrateAsync();
            Log.Information("Migration 執行完成");
        }
        else
        {
            Log.Information("資料庫已是最新狀態");
        }
        
        Log.Information("資料庫初始化完成");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "資料庫初始化失敗");
        throw;
    }
}