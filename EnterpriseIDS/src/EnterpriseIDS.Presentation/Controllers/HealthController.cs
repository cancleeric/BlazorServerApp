using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Presentation.Controllers;

/// <summary>
/// 健康檢查控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IHealthCheckService _healthCheckService;

    public HealthController(
        ILogger<HealthController> logger,
        IHealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// 取得系統健康狀態
    /// </summary>
    /// <returns>健康檢查結果</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _healthCheckService.CheckHealthAsync(cancellationToken);
            
            return health.Status switch
            {
                HealthStatus.Healthy => Ok(health),
                HealthStatus.Degraded => StatusCode(200, health), // 降級但仍可服務
                HealthStatus.Unhealthy => StatusCode(503, health), // 服務不可用
                _ => StatusCode(503, health)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康檢查端點執行失敗");
            
            var errorResponse = new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "系統健康檢查",
                Description = "健康檢查執行失敗",
                ErrorMessage = ex.Message,
                CheckTime = DateTime.UtcNow
            };
            
            return StatusCode(503, errorResponse);
        }
    }

    /// <summary>
    /// 取得詳細健康檢查摘要
    /// </summary>
    /// <returns>詳細健康檢查摘要</returns>
    [HttpGet("summary")]
    [AllowAnonymous]
    public async Task<ActionResult<HealthCheckSummary>> GetHealthSummary(CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _healthCheckService.GetHealthSummaryAsync(cancellationToken);
            
            return summary.OverallStatus switch
            {
                HealthStatus.Healthy => Ok(summary),
                HealthStatus.Degraded => StatusCode(200, summary),
                HealthStatus.Unhealthy => StatusCode(503, summary),
                _ => StatusCode(503, summary)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康檢查摘要端點執行失敗");
            
            var errorSummary = new HealthCheckSummary
            {
                OverallStatus = HealthStatus.Unhealthy,
                CheckTime = DateTime.UtcNow,
                ServiceVersion = "Unknown"
            };
            
            return StatusCode(503, errorSummary);
        }
    }

    /// <summary>
    /// 檢查系統就緒狀態 (Kubernetes Readiness Probe)
    /// </summary>
    /// <returns>就緒狀態</returns>
    [HttpGet("ready")]
    [AllowAnonymous]
    public async Task<ActionResult<HealthCheckResponse>> GetReadiness(CancellationToken cancellationToken = default)
    {
        try
        {
            var readiness = await _healthCheckService.CheckReadinessAsync(cancellationToken);
            
            return readiness.Status == HealthStatus.Healthy 
                ? Ok(readiness)
                : StatusCode(503, readiness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "就緒狀態檢查失敗");
            
            var errorResponse = new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "就緒狀態檢查",
                Description = "就緒狀態檢查執行失敗",
                ErrorMessage = ex.Message,
                CheckTime = DateTime.UtcNow
            };
            
            return StatusCode(503, errorResponse);
        }
    }

    /// <summary>
    /// 檢查系統存活狀態 (Kubernetes Liveness Probe)
    /// </summary>
    /// <returns>存活狀態</returns>
    [HttpGet("live")]
    [AllowAnonymous]
    public async Task<ActionResult<HealthCheckResponse>> GetLiveness(CancellationToken cancellationToken = default)
    {
        try
        {
            var liveness = await _healthCheckService.CheckLivenessAsync(cancellationToken);
            
            return liveness.Status == HealthStatus.Healthy 
                ? Ok(liveness)
                : StatusCode(503, liveness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "存活狀態檢查失敗");
            
            var errorResponse = new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "存活狀態檢查",
                Description = "存活狀態檢查執行失敗",
                ErrorMessage = ex.Message,
                CheckTime = DateTime.UtcNow
            };
            
            return StatusCode(503, errorResponse);
        }
    }

    /// <summary>
    /// 檢查特定組件健康狀態
    /// </summary>
    /// <param name="component">組件名稱</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>組件健康狀態</returns>
    [HttpGet("component/{component}")]
    [AllowAnonymous]
    public async Task<ActionResult<HealthCheckResponse>> GetComponentHealth(
        string component, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _healthCheckService.CheckComponentHealthAsync(component, cancellationToken);
            
            return health.Status switch
            {
                HealthStatus.Healthy => Ok(health),
                HealthStatus.Degraded => StatusCode(200, health),
                HealthStatus.Unhealthy => StatusCode(503, health),
                _ => StatusCode(503, health)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "組件 {Component} 健康檢查失敗", component);
            
            var errorResponse = new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = component,
                Description = $"組件 {component} 健康檢查執行失敗",
                ErrorMessage = ex.Message,
                CheckTime = DateTime.UtcNow
            };
            
            return StatusCode(503, errorResponse);
        }
    }

    /// <summary>
    /// 簡單的 ping 端點
    /// </summary>
    /// <returns>Pong 回應</returns>
    [HttpGet("ping")]
    [AllowAnonymous]
    public ActionResult<object> Ping()
    {
        return Ok(new 
        { 
            message = "pong",
            timestamp = DateTime.UtcNow,
            server = Environment.MachineName,
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "Unknown"
        });
    }

    /// <summary>
    /// 取得系統版本資訊
    /// </summary>
    /// <returns>版本資訊</returns>
    [HttpGet("version")]
    [AllowAnonymous]
    public ActionResult<object> GetVersion()
    {
        try
        {
            var assembly = typeof(HealthController).Assembly;
            var version = assembly.GetName().Version;
            var fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);

            return Ok(new
            {
                ServiceName = "EnterpriseIdentityServer",
                Version = version?.ToString() ?? "Unknown",
                FileVersion = fileVersion.FileVersion ?? "Unknown",
                ProductVersion = fileVersion.ProductVersion ?? "Unknown",
                BuildDate = System.IO.File.GetCreationTime(assembly.Location),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                Framework = Environment.Version.ToString(),
                OS = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                StartTime = Process.GetCurrentProcess().StartTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得版本資訊失敗");
            return StatusCode(500, new { error = "無法取得版本資訊" });
        }
    }
}