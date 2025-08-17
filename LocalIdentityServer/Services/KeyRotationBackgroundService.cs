using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace LocalIdentityServer.Services;

/// <summary>
/// 金鑰輪替背景服務 - 自動執行金鑰輪替和健康檢查
/// 遵循單一責任原則 (SRP)
/// </summary>
public class KeyRotationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KeyRotationBackgroundService> _logger;
    private readonly KeyManagementOptions _options;
    private readonly PeriodicTimer _timer;

    public KeyRotationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<KeyRotationBackgroundService> logger,
        IOptions<KeyManagementOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // 設定定時器間隔
        var interval = TimeSpan.FromHours(_options.RotationCheckIntervalHours);
        _timer = new PeriodicTimer(interval);

        _logger.LogInformation("Key rotation background service initialized with {IntervalHours}h interval", 
            _options.RotationCheckIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAutomaticRotation)
        {
            _logger.LogInformation("Automatic key rotation is disabled");
            return;
        }

        _logger.LogInformation("Key rotation background service started");

        try
        {
            // 初始延遲，避免啟動時立即執行
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            // 執行初始檢查
            await PerformKeyMaintenanceAsync(stoppingToken);

            // 定期執行維護任務
            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                await PerformKeyMaintenanceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Key rotation background service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in key rotation background service");
        }
    }

    private async Task PerformKeyMaintenanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting key maintenance cycle");

            using var scope = _serviceProvider.CreateScope();
            var keyManagementService = scope.ServiceProvider.GetRequiredService<IKeyManagementService>();

            // 1. 執行健康檢查
            await PerformHealthCheckAsync(keyManagementService, cancellationToken);

            // 2. 檢查並執行金鑰輪替
            await CheckAndRotateKeysAsync(keyManagementService, cancellationToken);

            // 3. 清理過期金鑰
            await CleanupExpiredKeysAsync(keyManagementService, cancellationToken);

            // 4. 記錄統計資訊
            await LogKeyStatisticsAsync(keyManagementService, cancellationToken);

            _logger.LogDebug("Key maintenance cycle completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key maintenance cycle");
        }
    }

    private async Task PerformHealthCheckAsync(IKeyManagementService keyManagementService, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Performing key health check");

            var healthResult = await keyManagementService.CheckKeyHealthAsync();

            if (!healthResult.IsHealthy)
            {
                _logger.LogWarning("Key health check failed with {IssueCount} issues: {Issues}",
                    healthResult.Issues.Count, string.Join(", ", healthResult.Issues));

                // 如果沒有主要金鑰，嘗試強制輪替
                if (healthResult.Issues.Any(i => i.Contains("No primary signing key")))
                {
                    _logger.LogWarning("No primary signing key found, forcing key rotation");
                    await keyManagementService.ForceKeyRotationAsync();
                }
            }

            if (healthResult.Warnings.Any())
            {
                _logger.LogInformation("Key health check warnings: {Warnings}",
                    string.Join(", ", healthResult.Warnings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key health check");
        }
    }

    private async Task CheckAndRotateKeysAsync(IKeyManagementService keyManagementService, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Checking if key rotation is needed");

            var rotated = await keyManagementService.RotateKeyIfNeededAsync();
            if (rotated)
            {
                _logger.LogInformation("Automatic key rotation completed");

                // 發送通知 (可以擴展為發送郵件、Slack 等)
                await NotifyKeyRotationAsync("Automatic key rotation completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key rotation check");

            // 發送錯誤通知
            await NotifyKeyRotationAsync($"Key rotation failed: {ex.Message}");
        }
    }

    private async Task CleanupExpiredKeysAsync(IKeyManagementService keyManagementService, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Cleaning up expired keys");

            var cleanedCount = await keyManagementService.CleanupExpiredKeysAsync();
            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired keys", cleanedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expired keys cleanup");
        }
    }

    private async Task LogKeyStatisticsAsync(IKeyManagementService keyManagementService, CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await keyManagementService.GetKeyStatisticsAsync();

            _logger.LogInformation("Key statistics: {ActiveKeys} active, {ExpiredKeys} expired, {RevokedKeys} revoked. Next rotation: {NextRotation}",
                statistics.ActiveKeys, statistics.ExpiredKeys, statistics.RevokedKeys,
                statistics.NextRotationTime?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Not scheduled");

            // 如果主要金鑰即將到期，記錄警告
            if (statistics.PrimaryKey?.ExpiresAt.HasValue == true)
            {
                var daysUntilExpiration = (statistics.PrimaryKey.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
                if (daysUntilExpiration <= _options.RotationWarningDays)
                {
                    _logger.LogWarning("Primary key will expire in {Days} days (KeyId: {KeyId})",
                        Math.Ceiling(daysUntilExpiration), statistics.PrimaryKey.KeyId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging key statistics");
        }
    }

    private async Task NotifyKeyRotationAsync(string message)
    {
        try
        {
            // 這裡可以擴展為發送 email、Slack、Teams 等通知
            // 目前僅記錄到日誌
            _logger.LogInformation("Key rotation notification: {Message}", message);

            // 未來可以添加：
            // - Email 通知
            // - Slack webhook
            // - Azure Service Bus 訊息
            // - 其他通知系統

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending key rotation notification");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Key rotation background service stopping");
        
        _timer?.Dispose();
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("Key rotation background service stopped");
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}