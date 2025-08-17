using LocalIdentityServer.Services;
using LocalIdentityServer.Models;
using System.Text.Json;

namespace LocalIdentityServer.Middleware;

/// <summary>
/// MFA 專用 Rate Limiting 中介軟體
/// 為 MFA 端點提供額外的安全防護，包括暴力破解防護和異常行為偵測
/// 遵循單一責任原則 (SRP) - 專責處理 MFA 相關的速率限制
/// </summary>
public class MfaRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<MfaRateLimitingMiddleware> _logger;
    private readonly HashSet<string> _mfaEndpoints;

    public MfaRateLimitingMiddleware(
        RequestDelegate next,
        IRateLimitingService rateLimitingService,
        ILogger<MfaRateLimitingMiddleware> logger)
    {
        _next = next;
        _rateLimitingService = rateLimitingService;
        _logger = logger;
        
        // 定義需要 MFA Rate Limiting 的端點
        _mfaEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/mfa/verify",
            "/api/mfa/challenge",
            "/api/mfa/setup",
            "/api/mfa/disable",
            "/api/mfa/backup-codes",
            "/api/auth/token" // 當使用 MFA grant type 時
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 檢查是否為 MFA 相關端點
        if (!IsMfaEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            // 建立 Rate Limit 識別符
            var identifier = CreateRateLimitIdentifier(context);

            // 檢查 Rate Limit
            var rateLimitResult = await _rateLimitingService.CheckRateLimitAsync(identifier);

            if (!rateLimitResult.IsAllowed)
            {
                await HandleRateLimitExceeded(context, rateLimitResult);
                return;
            }

            // 設定響應標頭
            SetRateLimitHeaders(context.Response, rateLimitResult);

            // 繼續處理請求
            await _next(context);

            // 記錄成功的請求
            await _rateLimitingService.RecordRequestAsync(identifier, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MFA rate limiting middleware for path: {Path}", context.Request.Path);
            
            // 發生錯誤時不阻擋請求，但記錄錯誤
            await _next(context);
        }
    }

    private bool IsMfaEndpoint(PathString path)
    {
        return _mfaEndpoints.Any(endpoint => 
            path.StartsWithSegments(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    private RateLimitIdentifier CreateRateLimitIdentifier(HttpContext context)
    {
        var userId = context.User?.FindFirst("sub")?.Value ??
                    context.User?.FindFirst("user_id")?.Value;

        return new RateLimitIdentifier
        {
            IpAddress = GetClientIpAddress(context),
            UserId = userId,
            EndpointPath = context.Request.Path,
            HttpMethod = context.Request.Method,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            SessionId = context.TraceIdentifier
        };
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // 處理反向代理的情況
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task HandleRateLimitExceeded(HttpContext context, RateLimitResult rateLimitResult)
    {
        _logger.LogWarning("Rate limit exceeded for IP: {IP}, Endpoint: {Endpoint}, Rule: {Rule}",
            rateLimitResult.Identifier.IpAddress,
            rateLimitResult.Identifier.EndpointPath,
            rateLimitResult.RuleName);

        // 設定響應
        context.Response.StatusCode = 429; // Too Many Requests
        context.Response.ContentType = "application/json";

        // 設定標頭
        SetRateLimitHeaders(context.Response, rateLimitResult);

        // 建立錯誤響應
        var errorResponse = new
        {
            error = "rate_limit_exceeded",
            error_description = rateLimitResult.ErrorMessage ?? "Too many requests",
            retry_after = rateLimitResult.RetryAfterSeconds,
            reset_time = rateLimitResult.ResetTime,
            remaining = rateLimitResult.Remaining,
            limit = rateLimitResult.Limit,
            level = rateLimitResult.TriggeredLevel.ToString(),
            rule_name = rateLimitResult.RuleName
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        await context.Response.WriteAsync(jsonResponse);

        // 記錄被拒絕的請求
        await _rateLimitingService.RecordRequestAsync(rateLimitResult.Identifier, false);

        // 檢查是否需要臨時加入黑名單
        await CheckAndApplyBlacklist(rateLimitResult);
    }

    private void SetRateLimitHeaders(HttpResponse response, RateLimitResult rateLimitResult)
    {
        response.Headers.Append("X-RateLimit-Limit", rateLimitResult.Limit.ToString());
        response.Headers.Append("X-RateLimit-Remaining", rateLimitResult.Remaining.ToString());
        response.Headers.Append("X-RateLimit-Reset", rateLimitResult.ResetTime.ToString());

        if (!rateLimitResult.IsAllowed)
        {
            response.Headers.Append("Retry-After", rateLimitResult.RetryAfterSeconds.ToString());
            response.Headers.Append("X-RateLimit-Rule", rateLimitResult.RuleName ?? "unknown");
            response.Headers.Append("X-RateLimit-Level", rateLimitResult.TriggeredLevel.ToString());
        }
    }

    private async Task CheckAndApplyBlacklist(RateLimitResult rateLimitResult)
    {
        try
        {
            // 如果是嚴重的 Rate Limit 違規，考慮臨時黑名單
            if (rateLimitResult.TriggeredLevel == RateLimitLevel.IpLevel && 
                rateLimitResult.Remaining <= 0)
            {
                // 檢查最近的違規次數
                var status = await _rateLimitingService.GetCurrentStatusAsync(rateLimitResult.Identifier);
                
                // 如果多個層級都達到限制，進行臨時黑名單
                var violationCount = status.CurrentCounts.Values.Count(count => count >= status.Limits.Values.Max());
                
                if (violationCount >= 2) // 至少兩個層級違規
                {
                    await _rateLimitingService.BlacklistTemporaryAsync(
                        rateLimitResult.Identifier, 
                        15, // 15分鐘黑名單
                        "Multiple rate limit violations detected");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blacklist criteria for IP: {IP}", 
                rateLimitResult.Identifier.IpAddress);
        }
    }
}

/// <summary>
/// MFA Rate Limiting 中介軟體擴展方法
/// </summary>
public static class MfaRateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseMfaRateLimit(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MfaRateLimitingMiddleware>();
    }
}