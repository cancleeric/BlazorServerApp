using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EnterpriseIDS.Application.Services;

namespace EnterpriseIDS.Presentation.Controllers;

/// <summary>
/// 指標控制器 - 提供 Prometheus 指標和自訂指標端點
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly ILogger<MetricsController> _logger;
    private readonly MetricsCollectionService _metricsService;

    public MetricsController(
        ILogger<MetricsController> logger,
        MetricsCollectionService metricsService)
    {
        _logger = logger;
        _metricsService = metricsService;
    }

    /// <summary>
    /// 取得指標摘要
    /// </summary>
    /// <returns>指標摘要資訊</returns>
    [HttpGet("summary")]
    [AllowAnonymous]
    public ActionResult<Dictionary<string, object>> GetMetricsSummary()
    {
        try
        {
            var summary = _metricsService.GetMetricsSummary();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得指標摘要失敗");
            return StatusCode(500, new { error = "無法取得指標摘要" });
        }
    }

    /// <summary>
    /// 手動觸發系統指標更新
    /// </summary>
    /// <returns>更新結果</returns>
    [HttpPost("update-system")]
    [AllowAnonymous]
    public ActionResult UpdateSystemMetrics()
    {
        try
        {
            _metricsService.UpdateSystemMetrics();
            return Ok(new { message = "系統指標已更新", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系統指標失敗");
            return StatusCode(500, new { error = "無法更新系統指標" });
        }
    }

    /// <summary>
    /// 取得即時系統狀態
    /// </summary>
    /// <returns>系統狀態資訊</returns>
    [HttpGet("system-status")]
    [AllowAnonymous]
    public ActionResult<object> GetSystemStatus()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow.AddMilliseconds(-Environment.TickCount);

            var status = new
            {
                Server = new
                {
                    Name = Environment.MachineName,
                    OS = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    DotNetVersion = Environment.Version.ToString(),
                    WorkingDirectory = Environment.CurrentDirectory
                },
                Process = new
                {
                    Id = Environment.ProcessId,
                    StartTime = startTime,
                    Uptime = DateTime.UtcNow - startTime,
                    WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
                    PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
                    VirtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024,
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount
                },
                Memory = new
                {
                    GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                },
                Environment = new
                {
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    CurrentDirectory = Environment.CurrentDirectory,
                    SystemDirectory = Environment.SystemDirectory,
                    TickCount = Environment.TickCount64,
                    HasShutdownStarted = Environment.HasShutdownStarted
                },
                Timestamp = DateTime.UtcNow
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得系統狀態失敗");
            return StatusCode(500, new { error = "無法取得系統狀態" });
        }
    }

    /// <summary>
    /// 手動記錄測試指標 (用於測試目的)
    /// </summary>
    /// <param name="request">測試指標請求</param>
    /// <returns>記錄結果</returns>
    [HttpPost("test")]
    [AllowAnonymous]
    public ActionResult RecordTestMetrics([FromBody] TestMetricsRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("請提供有效的測試指標請求");
            }

            // 記錄各種測試指標
            if (!string.IsNullOrEmpty(request.AuthResult) && !string.IsNullOrEmpty(request.TenantId))
            {
                _metricsService.RecordAuthenticationAttempt(request.AuthResult, request.TenantId);
            }

            if (!string.IsNullOrEmpty(request.TokenType) && !string.IsNullOrEmpty(request.TenantId))
            {
                _metricsService.RecordTokenIssued(request.TokenType, request.TenantId);
            }

            if (!string.IsNullOrEmpty(request.Operation) && !string.IsNullOrEmpty(request.TenantId))
            {
                _metricsService.RecordTenantOperation(request.Operation, request.TenantId);
            }

            if (!string.IsNullOrEmpty(request.LdapOperation) && !string.IsNullOrEmpty(request.LdapResult))
            {
                var duration = TimeSpan.FromMilliseconds(request.DurationMs ?? 100);
                _metricsService.RecordLdapOperation(request.LdapOperation, request.LdapResult, duration);
            }

            if (request.ActiveSessions.HasValue && !string.IsNullOrEmpty(request.TenantId))
            {
                _metricsService.SetActiveSessions(request.ActiveSessions.Value, request.TenantId);
            }

            if (request.ActiveConnections.HasValue)
            {
                _metricsService.SetActiveConnections(request.ActiveConnections.Value);
            }

            return Ok(new { 
                message = "測試指標已記錄", 
                timestamp = DateTime.UtcNow,
                request = request
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄測試指標失敗");
            return StatusCode(500, new { error = "無法記錄測試指標" });
        }
    }
}

/// <summary>
/// 測試指標請求模型
/// </summary>
public class TestMetricsRequest
{
    /// <summary>
    /// 認證結果
    /// </summary>
    public string? AuthResult { get; set; }

    /// <summary>
    /// Token 類型
    /// </summary>
    public string? TokenType { get; set; }

    /// <summary>
    /// 租戶 ID
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 操作名稱
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// LDAP 操作
    /// </summary>
    public string? LdapOperation { get; set; }

    /// <summary>
    /// LDAP 結果
    /// </summary>
    public string? LdapResult { get; set; }

    /// <summary>
    /// 持續時間 (毫秒)
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// 活躍會話數
    /// </summary>
    public int? ActiveSessions { get; set; }

    /// <summary>
    /// 活躍連線數
    /// </summary>
    public int? ActiveConnections { get; set; }
}