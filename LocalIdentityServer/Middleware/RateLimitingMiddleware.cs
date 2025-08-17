using LocalIdentityServer.Models;
using LocalIdentityServer.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace LocalIdentityServer.Middleware;

/// <summary>
/// Rate Limiting 中間件 - 遵循單一責任原則 (SRP)
/// 負責攔截 HTTP 請求並執行多層級限流檢查
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimitingService rateLimitingService,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _rateLimitingService = rateLimitingService ?? throw new ArgumentNullException(nameof(rateLimitingService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 如果停用 Rate Limiting，直接通過
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        try
        {
            // 建立請求識別資訊
            var identifier = CreateIdentifier(context);

            // 檢查限流狀態
            var result = await _rateLimitingService.CheckRateLimitAsync(identifier);

            // 新增限流資訊到回應標頭
            AddRateLimitHeaders(context, result);

            if (!result.IsAllowed)
            {
                // 限流觸發，記錄日誌並回應錯誤
                await HandleRateLimitExceededAsync(context, result);
                return;
            }

            // 允許請求繼續處理
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limiting middleware for {Path}", context.Request.Path);
            
            // 錯誤時允許請求通過，避免服務中斷
            await _next(context);
        }
    }

    private RateLimitIdentifier CreateIdentifier(HttpContext context)
    {
        var identifier = new RateLimitIdentifier
        {
            IpAddress = GetClientIpAddress(context),
            EndpointPath = context.Request.Path.Value ?? string.Empty,
            HttpMethod = context.Request.Method,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            RequestTime = DateTime.UtcNow
        };

        // 從認證資訊取得使用者 ID
        if (context.User.Identity?.IsAuthenticated == true)
        {
            identifier.UserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? context.User.FindFirst("sub")?.Value;
            
            identifier.ClientId = context.User.FindFirst("client_id")?.Value;
        }

        // 從認證資訊取得租戶 ID (如果適用)
        identifier.TenantId = context.User.FindFirst("tenant_id")?.Value
                            ?? context.Request.Headers["X-Tenant-ID"].FirstOrDefault();

        return identifier;
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // 檢查 X-Forwarded-For 標頭 (代理伺服器)
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var firstIp = xForwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp) && IPAddress.TryParse(firstIp, out _))
            {
                return firstIp;
            }
        }

        // 檢查 X-Real-IP 標頭
        var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp) && IPAddress.TryParse(xRealIp, out _))
        {
            return xRealIp;
        }

        // 檢查 CF-Connecting-IP 標頭 (Cloudflare)
        var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cfConnectingIp) && IPAddress.TryParse(cfConnectingIp, out _))
        {
            return cfConnectingIp;
        }

        // 使用連線的遠端 IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void AddRateLimitHeaders(HttpContext context, RateLimitResult result)
    {
        if (!_options.Headers.IncludeHeaders)
            return;

        try
        {
            var headers = context.Response.Headers;

            // 限制數量
            headers[_options.Headers.LimitHeader] = result.Limit.ToString();

            // 剩餘數量
            headers[_options.Headers.RemainingHeader] = result.Remaining.ToString();

            // 重設時間 (Unix 時間戳)
            headers[_options.Headers.ResetHeader] = result.ResetTime.ToString();

            // 重試建議 (僅在限流時)
            if (!result.IsAllowed && result.RetryAfterSeconds.HasValue)
            {
                headers[_options.Headers.RetryAfterHeader] = result.RetryAfterSeconds.Value.ToString();
            }

            // 額外的限流資訊
            headers["X-RateLimit-Policy"] = result.RuleName ?? "default";
            headers["X-RateLimit-Level"] = result.TriggeredLevel.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add rate limit headers");
        }
    }

    private async Task HandleRateLimitExceededAsync(HttpContext context, RateLimitResult result)
    {
        try
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = "rate_limit_exceeded",
                error_description = result.ErrorMessage ?? _options.DefaultErrorMessage,
                rule_name = result.RuleName,
                triggered_level = result.TriggeredLevel.ToString().ToLower(),
                limit = result.Limit,
                remaining = result.Remaining,
                reset_time = result.ResetTime,
                retry_after = result.RetryAfterSeconds,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            await context.Response.WriteAsync(json);

            // 記錄限流事件
            if (_options.Monitoring.LogRateLimitEvents)
            {
                _logger.LogWarning("Rate limit exceeded: IP={IP}, User={User}, Endpoint={Endpoint}, Rule={Rule}, Level={Level}",
                    result.Identifier.IpAddress,
                    result.Identifier.UserId,
                    result.Identifier.EndpointPath,
                    result.RuleName,
                    result.TriggeredLevel);
            }

            // 檢查是否需要臨時封鎖
            await CheckAndApplyTemporaryBlockAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle rate limit exceeded response");
            
            // 設定基本的 429 回應
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
        }
    }

    private async Task CheckAndApplyTemporaryBlockAsync(RateLimitResult result)
    {
        try
        {
            // 實作自動黑名單邏輯：多次觸發限流時臨時封鎖
            var identifier = result.Identifier;
            var violationKey = $"violations:{identifier.IpAddress}";

            // 記錄違規次數
            var violations = await _rateLimitingService.GetCurrentStatusAsync(identifier);
            
            // 如果在短時間內多次違規，進行臨時封鎖
            // 這裡可以根據不同的威脅等級設置不同的封鎖策略
            if (result.TriggeredLevel == RateLimitLevel.UserLevel)
            {
                // 使用者層級違規，封鎖時間較短
                await _rateLimitingService.BlacklistTemporaryAsync(identifier, 5, 
                    $"Repeated user-level rate limit violations");
            }
            else if (result.TriggeredLevel == RateLimitLevel.IpLevel)
            {
                // IP 層級違規，封鎖時間較長
                await _rateLimitingService.BlacklistTemporaryAsync(identifier, 15, 
                    $"Repeated IP-level rate limit violations");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply temporary block for {IP}", result.Identifier.IpAddress);
        }
    }
}

/// <summary>
/// Rate Limiting 中間件擴展方法
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// 註冊 Rate Limiting 中間件
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        if (app == null)
            throw new ArgumentNullException(nameof(app));

        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    /// <summary>
    /// 註冊 Rate Limiting 服務
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, 
        IConfiguration configuration, bool useRedis = true)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // 註冊配置
        services.Configure<RateLimitingOptions>(
            configuration.GetSection(RateLimitingOptions.SectionName));

        // 註冊快取服務
        if (useRedis)
        {
            // 註冊 Redis 連線
            services.AddStackExchangeRedisCache(options =>
            {
                var rateLimitConfig = configuration.GetSection(RateLimitingOptions.SectionName)
                    .Get<RateLimitingOptions>();
                
                options.Configuration = rateLimitConfig?.RedisConnectionString ?? "localhost:6379";
            });

            // 註冊 Redis ConnectionMultiplexer
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(provider =>
            {
                var rateLimitConfig = provider.GetService<IOptions<RateLimitingOptions>>()?.Value;
                var connectionString = rateLimitConfig?.RedisConnectionString ?? "localhost:6379";
                
                return StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString);
            });

            services.AddScoped<IRateLimitingCacheService, RateLimitingCacheService>();
        }
        else
        {
            // 使用記憶體快取 (開發/測試用)
            services.AddScoped<IRateLimitingCacheService, InMemoryRateLimitingCacheService>();
        }

        // 註冊主要服務
        services.AddScoped<IRateLimitingService, RateLimitingService>();

        return services;
    }
}