using Microsoft.Extensions.Options;
using LocalIdentityServer.Models;
using System.Text;

namespace LocalIdentityServer.Middleware;

/// <summary>
/// 安全標頭中介軟體 - 遵循 SOLID 原則
/// 實作企業級 HTTPS 安全強化與完整安全標頭策略
/// 符合 OWASP 安全標準與最佳實務
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeadersOptions> options,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        try
        {
            // 在回應開始前設定安全標頭
            context.Response.OnStarting(() =>
            {
                SetSecurityHeaders(context);
                return Task.CompletedTask;
            });

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in SecurityHeadersMiddleware");
            throw;
        }
    }

    /// <summary>
    /// 設定所有安全標頭
    /// </summary>
    private void SetSecurityHeaders(HttpContext context)
    {
        var response = context.Response;
        var request = context.Request;

        try
        {
            // 移除不安全的標頭
            RemoveUnsafeHeaders(response);

            // 設定 HSTS (僅在 HTTPS 連接時)
            if (request.IsHttps && _options.Hsts.Enabled)
            {
                SetHstsHeader(response);
            }

            // 設定 Content Security Policy
            if (_options.Csp.Enabled)
            {
                SetContentSecurityPolicyHeader(response);
            }

            // 設定 X-Frame-Options
            if (_options.FrameOptions.Enabled)
            {
                SetFrameOptionsHeader(response);
            }

            // 設定 X-Content-Type-Options
            if (_options.ContentTypeOptions.Enabled)
            {
                SetContentTypeOptionsHeader(response);
            }

            // 設定 Referrer-Policy
            if (_options.ReferrerPolicy.Enabled)
            {
                SetReferrerPolicyHeader(response);
            }

            // 設定 Cross-Origin 政策標頭
            SetCrossOriginPolicyHeaders(response);

            // 設定 Permissions-Policy
            if (_options.PermissionsPolicy.Enabled)
            {
                SetPermissionsPolicyHeader(response);
            }

            // 設定自訂標頭
            SetCustomHeaders(response);

            _logger.LogDebug("Security headers applied successfully for {Path}", 
                context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set security headers for {Path}", 
                context.Request.Path);
        }
    }

    /// <summary>
    /// 移除不安全的標頭
    /// </summary>
    private void RemoveUnsafeHeaders(HttpResponse response)
    {
        foreach (var header in _options.RemoveHeaders)
        {
            response.Headers.Remove(header);
        }
    }

    /// <summary>
    /// 設定 HSTS 標頭
    /// </summary>
    private void SetHstsHeader(HttpResponse response)
    {
        var hstsValue = new StringBuilder();
        hstsValue.Append($"max-age={_options.Hsts.MaxAge}");

        if (_options.Hsts.IncludeSubDomains)
        {
            hstsValue.Append("; includeSubDomains");
        }

        if (_options.Hsts.Preload)
        {
            hstsValue.Append("; preload");
        }

        response.Headers.TryAdd("Strict-Transport-Security", hstsValue.ToString());
    }

    /// <summary>
    /// 設定 Content Security Policy 標頭
    /// </summary>
    private void SetContentSecurityPolicyHeader(HttpResponse response)
    {
        var cspValue = BuildContentSecurityPolicy();
        var headerName = _options.Csp.ReportOnly ? 
            "Content-Security-Policy-Report-Only" : 
            "Content-Security-Policy";

        response.Headers.TryAdd(headerName, cspValue);
    }

    /// <summary>
    /// 建構 CSP 政策字串
    /// </summary>
    private string BuildContentSecurityPolicy()
    {
        var policies = new List<string>();

        // 標準 CSP 指令
        AddCspDirective(policies, "default-src", _options.Csp.DefaultSrc);
        AddCspDirective(policies, "script-src", _options.Csp.ScriptSrc);
        AddCspDirective(policies, "style-src", _options.Csp.StyleSrc);
        AddCspDirective(policies, "img-src", _options.Csp.ImgSrc);
        AddCspDirective(policies, "font-src", _options.Csp.FontSrc);
        AddCspDirective(policies, "connect-src", _options.Csp.ConnectSrc);
        AddCspDirective(policies, "media-src", _options.Csp.MediaSrc);
        AddCspDirective(policies, "object-src", _options.Csp.ObjectSrc);
        AddCspDirective(policies, "child-src", _options.Csp.ChildSrc);
        AddCspDirective(policies, "frame-ancestors", _options.Csp.FrameAncestors);
        AddCspDirective(policies, "base-uri", _options.Csp.BaseUri);
        AddCspDirective(policies, "form-action", _options.Csp.FormAction);

        // 報告端點
        if (!string.IsNullOrWhiteSpace(_options.Csp.ReportUri))
        {
            AddCspDirective(policies, "report-uri", _options.Csp.ReportUri);
        }

        // 自訂指令
        foreach (var customDirective in _options.Csp.CustomDirectives)
        {
            AddCspDirective(policies, customDirective.Key, customDirective.Value);
        }

        return string.Join("; ", policies);
    }

    /// <summary>
    /// 新增 CSP 指令
    /// </summary>
    private void AddCspDirective(List<string> policies, string directive, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            policies.Add($"{directive} {value}");
        }
    }

    /// <summary>
    /// 設定 X-Frame-Options 標頭
    /// </summary>
    private void SetFrameOptionsHeader(HttpResponse response)
    {
        var value = _options.FrameOptions.Policy.ToUpperInvariant() switch
        {
            "ALLOW-FROM" when !string.IsNullOrWhiteSpace(_options.FrameOptions.AllowFrom) => 
                $"ALLOW-FROM {_options.FrameOptions.AllowFrom}",
            "SAMEORIGIN" => "SAMEORIGIN",
            _ => "DENY"
        };

        response.Headers.TryAdd("X-Frame-Options", value);
    }

    /// <summary>
    /// 設定 X-Content-Type-Options 標頭
    /// </summary>
    private void SetContentTypeOptionsHeader(HttpResponse response)
    {
        response.Headers.TryAdd("X-Content-Type-Options", _options.ContentTypeOptions.Value);
    }

    /// <summary>
    /// 設定 Referrer-Policy 標頭
    /// </summary>
    private void SetReferrerPolicyHeader(HttpResponse response)
    {
        response.Headers.TryAdd("Referrer-Policy", _options.ReferrerPolicy.Policy);
    }

    /// <summary>
    /// 設定 Cross-Origin 政策標頭
    /// </summary>
    private void SetCrossOriginPolicyHeaders(HttpResponse response)
    {
        // Cross-Origin-Embedder-Policy
        if (_options.CrossOriginEmbedderPolicy.Enabled)
        {
            response.Headers.TryAdd("Cross-Origin-Embedder-Policy", 
                _options.CrossOriginEmbedderPolicy.Policy);
        }

        // Cross-Origin-Opener-Policy
        if (_options.CrossOriginOpenerPolicy.Enabled)
        {
            response.Headers.TryAdd("Cross-Origin-Opener-Policy", 
                _options.CrossOriginOpenerPolicy.Policy);
        }

        // Cross-Origin-Resource-Policy
        if (_options.CrossOriginResourcePolicy.Enabled)
        {
            response.Headers.TryAdd("Cross-Origin-Resource-Policy", 
                _options.CrossOriginResourcePolicy.Policy);
        }
    }

    /// <summary>
    /// 設定 Permissions-Policy 標頭
    /// </summary>
    private void SetPermissionsPolicyHeader(HttpResponse response)
    {
        if (_options.PermissionsPolicy.Policies.Any())
        {
            var policies = _options.PermissionsPolicy.Policies
                .Select(p => $"{p.Key}={p.Value}")
                .ToList();

            var policyValue = string.Join(", ", policies);
            response.Headers.TryAdd("Permissions-Policy", policyValue);
        }
    }

    /// <summary>
    /// 設定自訂標頭
    /// </summary>
    private void SetCustomHeaders(HttpResponse response)
    {
        foreach (var customHeader in _options.CustomHeaders)
        {
            response.Headers.TryAdd(customHeader.Key, customHeader.Value);
        }
    }
}

/// <summary>
/// SecurityHeadersMiddleware 擴充方法
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// 註冊安全標頭中介軟體
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}