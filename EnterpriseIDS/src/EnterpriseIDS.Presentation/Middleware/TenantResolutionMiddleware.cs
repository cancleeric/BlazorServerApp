using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.Services;

namespace EnterpriseIDS.Presentation.Middleware;

/// <summary>
/// 租戶解析中介軟體
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver, ITenantContextService tenantContextService)
    {
        try
        {
            // 跳過特定路徑的租戶解析
            if (ShouldSkipTenantResolution(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // 從請求中解析租戶
            var tenant = await ResolveTenantFromRequest(context, tenantResolver);

            if (tenant != null)
            {
                // 設定租戶上下文
                if (tenantContextService is TenantContextService contextService)
                {
                    var tenantContext = TenantContext.FromTenant(tenant);
                    contextService.SetCurrentTenantContext(tenantContext);
                }
                else
                {
                    tenantContextService.SetCurrentTenant(tenant.Id);
                }

                _logger.LogDebug("租戶解析成功: {TenantSlug} ({TenantId})", tenant.Slug, tenant.Id);

                // 添加租戶資訊到回應標頭
                context.Response.Headers["X-Tenant-ID"] = tenant.Id.ToString();
                context.Response.Headers["X-Tenant-Slug"] = tenant.Slug;
            }
            else
            {
                _logger.LogDebug("未解析到租戶，使用無租戶模式");
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "租戶解析中介軟體發生錯誤");
            
            // 清除租戶上下文
            tenantContextService.SetCurrentTenant(null);
            
            // 繼續處理請求，但不設定租戶上下文
            await _next(context);
        }
    }

    /// <summary>
    /// 從請求中解析租戶
    /// </summary>
    private async Task<Core.Entities.Tenant?> ResolveTenantFromRequest(HttpContext context, ITenantResolver tenantResolver)
    {
        var request = context.Request;

        // 準備解析參數
        var host = request.Host.Value;
        var path = request.Path.Value;
        var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        // 執行租戶解析
        return await tenantResolver.ResolveAsync(host, path, headers);
    }

    /// <summary>
    /// 判斷是否應該跳過租戶解析
    /// </summary>
    private static bool ShouldSkipTenantResolution(PathString path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/healthz",
            "/ready",
            "/live",
            "/metrics",
            "/swagger",
            "/api/system",
            "/api/health",
            "/.well-known",
            "/favicon.ico",
            "/robots.txt"
        };

        return skipPaths.Any(skipPath => 
            path.StartsWithSegments(skipPath, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 租戶解析中介軟體擴展方法
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>
    /// 添加租戶解析中介軟體
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }
}

/// <summary>
/// 租戶權限中介軟體
/// </summary>
public class TenantAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantAuthorizationMiddleware> _logger;

    public TenantAuthorizationMiddleware(RequestDelegate next, ILogger<TenantAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextService tenantContextService)
    {
        try
        {
            // 檢查是否需要租戶權限驗證
            if (RequiresTenantAuthorization(context))
            {
                var currentTenantId = tenantContextService.GetCurrentTenantId();
                
                // 從路由參數中獲取請求的租戶 ID
                var requestedTenantId = GetRequestedTenantId(context);

                if (requestedTenantId.HasValue)
                {
                    // 檢查是否有權限存取請求的租戶
                    if (!tenantContextService.IsSuperAdminContext() && 
                        !tenantContextService.HasTenantAccess(requestedTenantId.Value))
                    {
                        _logger.LogWarning("租戶權限不足: 當前租戶 {CurrentTenantId}, 請求租戶 {RequestedTenantId}", 
                            currentTenantId, requestedTenantId);

                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("沒有權限存取此租戶資源");
                        return;
                    }
                }
                else if (currentTenantId == null && !tenantContextService.IsSuperAdminContext())
                {
                    // 需要租戶上下文但沒有設定
                    _logger.LogWarning("缺少租戶上下文");

                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("缺少租戶上下文");
                    return;
                }
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "租戶權限中介軟體發生錯誤");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 判斷是否需要租戶權限驗證
    /// </summary>
    private static bool RequiresTenantAuthorization(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(path))
            return false;

        // 需要租戶權限的 API 路徑
        var tenantProtectedPaths = new[]
        {
            "/api/tenants/",
            "/api/users/",
            "/api/clients/",
            "/api/configurations/"
        };

        return tenantProtectedPaths.Any(protectedPath => path.StartsWith(protectedPath));
    }

    /// <summary>
    /// 從請求中獲取租戶 ID
    /// </summary>
    private static Guid? GetRequestedTenantId(HttpContext context)
    {
        // 從路由參數獲取
        if (context.Request.RouteValues.TryGetValue("tenantId", out var tenantIdValue) &&
            Guid.TryParse(tenantIdValue?.ToString(), out var tenantId))
        {
            return tenantId;
        }

        // 從查詢參數獲取
        if (context.Request.Query.TryGetValue("tenantId", out var queryTenantId) &&
            Guid.TryParse(queryTenantId.ToString(), out var queryParsedTenantId))
        {
            return queryParsedTenantId;
        }

        // 從請求標頭獲取
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId) &&
            Guid.TryParse(headerTenantId.ToString(), out var headerParsedTenantId))
        {
            return headerParsedTenantId;
        }

        return null;
    }
}

/// <summary>
/// 租戶權限中介軟體擴展方法
/// </summary>
public static class TenantAuthorizationMiddlewareExtensions
{
    /// <summary>
    /// 添加租戶權限中介軟體
    /// </summary>
    public static IApplicationBuilder UseTenantAuthorization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantAuthorizationMiddleware>();
    }
}

/// <summary>
/// 租戶資訊中介軟體 - 添加租戶資訊到日誌和追蹤
/// </summary>
public class TenantLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantLoggingMiddleware> _logger;

    public TenantLoggingMiddleware(RequestDelegate next, ILogger<TenantLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextService tenantContextService)
    {
        var tenantId = tenantContextService.GetCurrentTenantId();
        
        // 添加租戶資訊到日誌範圍
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = tenantId?.ToString() ?? "none",
            ["RequestId"] = context.TraceIdentifier,
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["RemoteIP"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        });

        // 添加租戶資訊到回應標頭（用於除錯）
        if (tenantId.HasValue)
        {
            context.Response.Headers["X-Debug-Tenant-ID"] = tenantId.ToString();
        }

        var startTime = DateTime.UtcNow;
        
        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("請求完成: {Method} {Path} - {StatusCode} - {Duration}ms - Tenant: {TenantId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                duration.TotalMilliseconds,
                tenantId?.ToString() ?? "none");
        }
    }
}

/// <summary>
/// 租戶日誌中介軟體擴展方法
/// </summary>
public static class TenantLoggingMiddlewareExtensions
{
    /// <summary>
    /// 添加租戶日誌中介軟體
    /// </summary>
    public static IApplicationBuilder UseTenantLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantLoggingMiddleware>();
    }
}