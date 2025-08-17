using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Infrastructure.Middleware;

/// <summary>
/// 分散式會話管理中介軟體
/// </summary>
public class DistributedSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DistributedSessionMiddleware> _logger;
    private readonly DistributedSessionOptions _options;

    public DistributedSessionMiddleware(
        RequestDelegate next,
        ILogger<DistributedSessionMiddleware> logger,
        IOptions<DistributedSessionOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 處理請求的中介軟體邏輯
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IDistributedSessionService sessionService)
    {
        // 跳過不需要會話管理的路徑
        if (ShouldSkipSessionHandling(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            // 從請求中取得會話 ID
            var sessionId = GetSessionIdFromRequest(context);
            
            if (!string.IsNullOrEmpty(sessionId))
            {
                // 驗證現有會話
                await ValidateAndRefreshSession(context, sessionService, sessionId);
            }
            else if (context.User.Identity?.IsAuthenticated == true)
            {
                // 為已認證使用者建立新會話
                await CreateNewSession(context, sessionService);
            }

            await _next(context);

            // 處理回應時的會話更新
            await HandleSessionOnResponse(context, sessionService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分散式會話中介軟體處理錯誤");
            await _next(context);
        }
    }

    /// <summary>
    /// 檢查是否應跳過會話處理
    /// </summary>
    private bool ShouldSkipSessionHandling(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(pathValue))
            return true;

        // 跳過靜態資源和API健康檢查
        return pathValue.StartsWith("/health") ||
               pathValue.StartsWith("/api/health") ||
               pathValue.StartsWith("/static") ||
               pathValue.StartsWith("/assets") ||
               pathValue.StartsWith("/css") ||
               pathValue.StartsWith("/js") ||
               pathValue.StartsWith("/images") ||
               pathValue.StartsWith("/favicon") ||
               pathValue.StartsWith("/_blazor") ||
               pathValue.Contains(".css") ||
               pathValue.Contains(".js") ||
               pathValue.Contains(".png") ||
               pathValue.Contains(".jpg") ||
               pathValue.Contains(".ico");
    }

    /// <summary>
    /// 從請求中取得會話 ID
    /// </summary>
    private string? GetSessionIdFromRequest(HttpContext context)
    {
        // 優先從 Cookie 取得
        var sessionId = context.Request.Cookies[_options.SessionCookieName];
        
        // 如果沒有 Cookie，嘗試從 Header 取得
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = context.Request.Headers[_options.SessionHeaderName].FirstOrDefault();
        }

        return sessionId;
    }

    /// <summary>
    /// 驗證並刷新會話
    /// </summary>
    private async Task ValidateAndRefreshSession(HttpContext context, IDistributedSessionService sessionService, string sessionId)
    {
        try
        {
            var sessionInfo = await sessionService.GetSessionAsync(sessionId);
            
            if (sessionInfo != null && !sessionInfo.IsExpired)
            {
                // 會話有效，刷新活動時間
                await sessionService.RefreshSessionAsync(sessionId);
                
                // 將會話資訊存入 HttpContext
                context.Items["SessionInfo"] = sessionInfo;
                context.Items["SessionId"] = sessionId;
                
                _logger.LogDebug("會話已驗證並刷新: {SessionId}", sessionId);
            }
            else
            {
                // 會話無效或過期，清除 Cookie
                ClearSessionCookie(context);
                _logger.LogDebug("會話無效或過期，已清除: {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "驗證會話時發生錯誤: {SessionId}", sessionId);
            ClearSessionCookie(context);
        }
    }

    /// <summary>
    /// 為已認證使用者建立新會話
    /// </summary>
    private async Task CreateNewSession(HttpContext context, IDistributedSessionService sessionService)
    {
        try
        {
            var userId = GetUserIdFromClaims(context.User);
            var tenantId = GetTenantIdFromClaims(context.User);
            
            if (userId != Guid.Empty && tenantId != Guid.Empty)
            {
                var sessionId = GenerateSessionId();
                var metadata = CreateSessionMetadata(context);
                
                await sessionService.CreateSessionAsync(
                    sessionId, 
                    userId, 
                    tenantId, 
                    metadata, 
                    TimeSpan.FromHours(_options.SessionTimeoutHours));

                // 設定會話 Cookie
                SetSessionCookie(context, sessionId);
                
                // 將會話資訊存入 HttpContext
                context.Items["SessionId"] = sessionId;
                
                _logger.LogInformation("已為使用者建立新會話: UserId={UserId}, SessionId={SessionId}", userId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立新會話時發生錯誤");
        }
    }

    /// <summary>
    /// 處理回應時的會話更新
    /// </summary>
    private async Task HandleSessionOnResponse(HttpContext context, IDistributedSessionService sessionService)
    {
        var sessionId = context.Items["SessionId"] as string;
        
        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                // 如果使用者登出，銷毀會話
                if (context.Response.Headers.ContainsKey("Location") && 
                    context.Response.Headers["Location"].ToString().Contains("/logout"))
                {
                    await sessionService.DestroySessionAsync(sessionId);
                    ClearSessionCookie(context);
                    _logger.LogInformation("使用者登出，已銷毀會話: {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "處理登出會話時發生錯誤: {SessionId}", sessionId);
            }
        }
    }

    /// <summary>
    /// 從 Claims 取得使用者 ID
    /// </summary>
    private Guid GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        return Guid.TryParse(userIdClaim?.Value, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// 從 Claims 取得租戶 ID
    /// </summary>
    private Guid GetTenantIdFromClaims(ClaimsPrincipal user)
    {
        var tenantIdClaim = user.FindFirst("tenant_id") ?? user.FindFirst("tid");
        return Guid.TryParse(tenantIdClaim?.Value, out var tenantId) ? tenantId : Guid.Empty;
    }

    /// <summary>
    /// 產生會話 ID
    /// </summary>
    private string GenerateSessionId()
    {
        return $"sess_{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    /// <summary>
    /// 建立會話元資料
    /// </summary>
    private Dictionary<string, object> CreateSessionMetadata(HttpContext context)
    {
        return new Dictionary<string, object>
        {
            ["UserAgent"] = context.Request.Headers["User-Agent"].ToString(),
            ["IpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            ["RequestPath"] = context.Request.Path.ToString(),
            ["CreatedAt"] = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 設定會話 Cookie
    /// </summary>
    private void SetSessionCookie(HttpContext context, string sessionId)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(_options.SessionTimeoutHours),
            Path = "/",
            Domain = _options.CookieDomain
        };

        context.Response.Cookies.Append(_options.SessionCookieName, sessionId, cookieOptions);
    }

    /// <summary>
    /// 清除會話 Cookie
    /// </summary>
    private void ClearSessionCookie(HttpContext context)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/",
            Domain = _options.CookieDomain
        };

        context.Response.Cookies.Append(_options.SessionCookieName, "", cookieOptions);
    }
}

/// <summary>
/// 分散式會話選項
/// </summary>
public class DistributedSessionOptions
{
    /// <summary>
    /// 會話 Cookie 名稱
    /// </summary>
    public string SessionCookieName { get; set; } = "EnterpriseIDS.SessionId";

    /// <summary>
    /// 會話 Header 名稱
    /// </summary>
    public string SessionHeaderName { get; set; } = "X-Session-ID";

    /// <summary>
    /// 會話逾時時數
    /// </summary>
    public int SessionTimeoutHours { get; set; } = 8;

    /// <summary>
    /// Cookie 網域
    /// </summary>
    public string? CookieDomain { get; set; }

    /// <summary>
    /// 自動清理過期會話間隔（分鐘）
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}

/// <summary>
/// 分散式會話中介軟體擴展方法
/// </summary>
public static class DistributedSessionMiddlewareExtensions
{
    /// <summary>
    /// 使用分散式會話中介軟體
    /// </summary>
    public static IApplicationBuilder UseDistributedSession(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DistributedSessionMiddleware>();
    }

    /// <summary>
    /// 使用分散式會話中介軟體並配置選項
    /// </summary>
    public static IApplicationBuilder UseDistributedSession(
        this IApplicationBuilder builder, 
        Action<DistributedSessionOptions> configureOptions)
    {
        var options = new DistributedSessionOptions();
        configureOptions(options);
        
        builder.ApplicationServices.GetService<IOptions<DistributedSessionOptions>>()?.Value.Apply(options);
        
        return builder.UseMiddleware<DistributedSessionMiddleware>();
    }
}

/// <summary>
/// 選項擴展方法
/// </summary>
internal static class OptionsExtensions
{
    public static void Apply(this DistributedSessionOptions target, DistributedSessionOptions source)
    {
        target.SessionCookieName = source.SessionCookieName;
        target.SessionHeaderName = source.SessionHeaderName;
        target.SessionTimeoutHours = source.SessionTimeoutHours;
        target.CookieDomain = source.CookieDomain;
        target.CleanupIntervalMinutes = source.CleanupIntervalMinutes;
    }
}