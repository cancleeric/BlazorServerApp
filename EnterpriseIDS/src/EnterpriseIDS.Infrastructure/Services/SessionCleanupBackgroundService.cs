using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Middleware;

namespace EnterpriseIDS.Infrastructure.Services;

/// <summary>
/// 會話清理背景服務
/// </summary>
public class SessionCleanupBackgroundService : BackgroundService
{
    private readonly IDistributedSessionService _sessionService;
    private readonly ILogger<SessionCleanupBackgroundService> _logger;
    private readonly DistributedSessionOptions _options;

    public SessionCleanupBackgroundService(
        IDistributedSessionService sessionService,
        ILogger<SessionCleanupBackgroundService> logger,
        IOptions<DistributedSessionOptions> options)
    {
        _sessionService = sessionService;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 執行背景服務
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);
        
        _logger.LogInformation("會話清理背景服務已啟動，清理間隔: {Interval} 分鐘", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessions(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("會話清理背景服務已取消");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "會話清理過程中發生錯誤");
                
                // 發生錯誤時等待較短時間後重試
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("會話清理背景服務已停止");
    }

    /// <summary>
    /// 清理過期會話
    /// </summary>
    private async Task CleanupExpiredSessions(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("開始清理過期會話");
            
            var startTime = DateTime.UtcNow;
            
            // 執行清理
            await _sessionService.CleanupExpiredSessionsAsync(cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("過期會話清理完成，耗時: {Duration}ms", duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理過期會話時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 停止服務時的清理工作
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止會話清理背景服務...");
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("會話清理背景服務已停止");
    }
}