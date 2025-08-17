using System.Diagnostics;
using EnterpriseIDS.Application.Services;

namespace EnterpriseIDS.Presentation.Middleware;

/// <summary>
/// HTTP 指標收集中介軟體
/// </summary>
public class HttpMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpMetricsMiddleware> _logger;
    private readonly MetricsCollectionService _metricsService;

    public HttpMetricsMiddleware(
        RequestDelegate next,
        ILogger<HttpMetricsMiddleware> logger,
        MetricsCollectionService metricsService)
    {
        _next = next;
        _logger = logger;
        _metricsService = metricsService;
    }

    /// <summary>
    /// 處理 HTTP 請求並收集指標
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = GetNormalizedPath(context.Request.Path);

        try
        {
            // 執行下一個中介軟體
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            try
            {
                // 記錄 HTTP 請求指標
                var statusCode = context.Response.StatusCode;
                var duration = stopwatch.Elapsed;
                
                _metricsService.RecordHttpRequest(method, path, statusCode, duration);
                
                _logger.LogDebug("HTTP 請求指標已記錄: {Method} {Path} -> {StatusCode} ({Duration}ms)",
                    method, path, statusCode, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "記錄 HTTP 請求指標時發生錯誤");
            }
        }
    }

    /// <summary>
    /// 標準化路徑，移除查詢參數和動態部分
    /// </summary>
    private static string GetNormalizedPath(PathString path)
    {
        var pathValue = path.Value ?? "/";
        
        // 移除查詢參數
        var queryIndex = pathValue.IndexOf('?');
        if (queryIndex >= 0)
        {
            pathValue = pathValue[..queryIndex];
        }
        
        // 標準化常見的動態路徑
        if (pathValue.StartsWith("/api/"))
        {
            var segments = pathValue.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // 替換可能的 ID 參數
            for (int i = 0; i < segments.Length; i++)
            {
                if (IsLikelyId(segments[i]))
                {
                    segments[i] = "{id}";
                }
            }
            
            return "/" + string.Join("/", segments);
        }
        
        return pathValue;
    }

    /// <summary>
    /// 判斷字串是否像是 ID 參數
    /// </summary>
    private static bool IsLikelyId(string segment)
    {
        // GUID 格式
        if (Guid.TryParse(segment, out _))
            return true;
            
        // 純數字
        if (int.TryParse(segment, out _))
            return true;
            
        // 長度較長的英數字字串 (可能是 ID)
        if (segment.Length > 8 && segment.All(c => char.IsLetterOrDigit(c)))
            return true;
            
        return false;
    }
}