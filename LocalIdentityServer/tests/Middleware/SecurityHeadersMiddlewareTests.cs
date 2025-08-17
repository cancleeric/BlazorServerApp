using LocalIdentityServer.Middleware;
using LocalIdentityServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;

namespace LocalIdentityServer.Tests.Middleware;

/// <summary>
/// SecurityHeadersMiddleware 單元測試
/// 驗證安全標頭中介軟體的完整功能與配置
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> _mockLogger;
    private readonly SecurityHeadersOptions _defaultOptions;
    private readonly Mock<RequestDelegate> _mockNext;

    public SecurityHeadersMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
        _mockNext = new Mock<RequestDelegate>();
        
        _defaultOptions = new SecurityHeadersOptions
        {
            Enabled = true,
            Hsts = new HstsOptions
            {
                Enabled = true,
                MaxAge = 31536000,
                IncludeSubDomains = true,
                Preload = false
            },
            Csp = new ContentSecurityPolicyOptions
            {
                Enabled = true,
                ReportOnly = false,
                DefaultSrc = "'self'",
                ScriptSrc = "'self' 'unsafe-inline'",
                StyleSrc = "'self' 'unsafe-inline'"
            },
            FrameOptions = new FrameOptions
            {
                Enabled = true,
                Policy = "DENY"
            },
            ContentTypeOptions = new ContentTypeOptions
            {
                Enabled = true,
                Value = "nosniff"
            },
            ReferrerPolicy = new ReferrerPolicyOptions
            {
                Enabled = true,
                Policy = "strict-origin-when-cross-origin"
            },
            CustomHeaders = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "test-value"
            },
            RemoveHeaders = new List<string> { "Server", "X-Powered-By" }
        };
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_ShouldNotSetSecurityHeaders()
    {
        // Arrange
        var options = new SecurityHeadersOptions { Enabled = false };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Empty(context.Response.Headers);
        _mockNext.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithDefaultConfiguration_ShouldSetAllSecurityHeaders()
    {
        // Arrange
        var middleware = CreateMiddleware(_defaultOptions);
        var context = CreateHttpContext(isHttps: true);

        // Act
        context = await InvokeMiddlewareAndStartResponse(middleware, context);

        // Assert
        var headers = context.Response.Headers;
        
        // 驗證 HSTS 標頭 (僅在 HTTPS 時設定)
        Assert.True(headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal("max-age=31536000; includeSubDomains", headers["Strict-Transport-Security"]);

        // 驗證 CSP 標頭
        Assert.True(headers.ContainsKey("Content-Security-Policy"));
        var cspValue = headers["Content-Security-Policy"].ToString();
        Assert.Contains("default-src 'self'", cspValue);
        Assert.Contains("script-src 'self' 'unsafe-inline'", cspValue);

        // 驗證其他安全標頭
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Equal("test-value", headers["X-Custom-Header"]);
    }

    [Fact]
    public async Task InvokeAsync_WithHttpConnection_ShouldNotSetHstsHeader()
    {
        // Arrange
        var middleware = CreateMiddleware(_defaultOptions);
        var context = CreateHttpContext(isHttps: false);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_WithCspReportOnly_ShouldSetReportOnlyHeader()
    {
        // Arrange
        var options = _defaultOptions;
        options.Csp.ReportOnly = true;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy-Report-Only"));
        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task InvokeAsync_WithCustomCspDirectives_ShouldIncludeCustomDirectives()
    {
        // Arrange
        var options = _defaultOptions;
        options.Csp.CustomDirectives["worker-src"] = "'self'";
        options.Csp.CustomDirectives["manifest-src"] = "'self'";
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var cspValue = context.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains("worker-src 'self'", cspValue);
        Assert.Contains("manifest-src 'self'", cspValue);
    }

    [Fact]
    public async Task InvokeAsync_WithFrameOptionsAllowFrom_ShouldSetCorrectValue()
    {
        // Arrange
        var options = _defaultOptions;
        options.FrameOptions.Policy = "ALLOW-FROM";
        options.FrameOptions.AllowFrom = "https://trusted.example.com";
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("ALLOW-FROM https://trusted.example.com", 
            context.Response.Headers["X-Frame-Options"]);
    }

    [Theory]
    [InlineData("SAMEORIGIN", "SAMEORIGIN")]
    [InlineData("DENY", "DENY")]
    [InlineData("invalid", "DENY")] // 預設為 DENY
    public async Task InvokeAsync_WithDifferentFrameOptionsPolicies_ShouldSetCorrectValue(
        string policy, string expectedValue)
    {
        // Arrange
        var options = _defaultOptions;
        options.FrameOptions.Policy = policy;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(expectedValue, context.Response.Headers["X-Frame-Options"]);
    }

    [Fact]
    public async Task InvokeAsync_WithCrossOriginPolicies_ShouldSetAllCrossOriginHeaders()
    {
        // Arrange
        var options = _defaultOptions;
        options.CrossOriginEmbedderPolicy = new CrossOriginEmbedderPolicyOptions
        {
            Enabled = true,
            Policy = "require-corp"
        };
        options.CrossOriginOpenerPolicy = new CrossOriginOpenerPolicyOptions
        {
            Enabled = true,
            Policy = "same-origin"
        };
        options.CrossOriginResourcePolicy = new CrossOriginResourcePolicyOptions
        {
            Enabled = true,
            Policy = "same-origin"
        };

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("require-corp", context.Response.Headers["Cross-Origin-Embedder-Policy"]);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"]);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"]);
    }

    [Fact]
    public async Task InvokeAsync_WithPermissionsPolicy_ShouldSetPermissionsPolicyHeader()
    {
        // Arrange
        var options = _defaultOptions;
        options.PermissionsPolicy = new PermissionsPolicyOptions
        {
            Enabled = true,
            Policies = new Dictionary<string, string>
            {
                ["geolocation"] = "()",
                ["camera"] = "(self)",
                ["microphone"] = "()"
            }
        };

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var permissionsPolicyValue = context.Response.Headers["Permissions-Policy"].ToString();
        Assert.Contains("geolocation=()", permissionsPolicyValue);
        Assert.Contains("camera=(self)", permissionsPolicyValue);
        Assert.Contains("microphone=()", permissionsPolicyValue);
    }

    [Fact]
    public async Task InvokeAsync_WithHstsPreload_ShouldIncludePreloadDirective()
    {
        // Arrange
        var options = _defaultOptions;
        options.Hsts.Preload = true;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext(isHttps: true);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var hstsValue = context.Response.Headers["Strict-Transport-Security"].ToString();
        Assert.Contains("preload", hstsValue);
        Assert.Contains("includeSubDomains", hstsValue);
        Assert.Contains("max-age=31536000", hstsValue);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRemoveConfiguredUnsafeHeaders()
    {
        // Arrange
        var middleware = CreateMiddleware(_defaultOptions);
        var context = CreateHttpContext();
        
        // 預先設定一些不安全的標頭
        context.Response.Headers["Server"] = "IIS/10.0";
        context.Response.Headers["X-Powered-By"] = "ASP.NET";
        context.Response.Headers["X-Safe-Header"] = "keep-this";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Server"));
        Assert.False(context.Response.Headers.ContainsKey("X-Powered-By"));
        Assert.True(context.Response.Headers.ContainsKey("X-Safe-Header"));
    }

    [Fact]
    public async Task InvokeAsync_WithDisabledFeatures_ShouldNotSetCorrespondingHeaders()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            Enabled = true,
            Hsts = new HstsOptions { Enabled = false },
            Csp = new ContentSecurityPolicyOptions { Enabled = false },
            FrameOptions = new FrameOptions { Enabled = false },
            ContentTypeOptions = new ContentTypeOptions { Enabled = false },
            ReferrerPolicy = new ReferrerPolicyOptions { Enabled = false }
        };

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext(isHttps: true);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.False(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.False(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
        Assert.False(context.Response.Headers.ContainsKey("Referrer-Policy"));
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccurs_ShouldLogErrorAndRethrow()
    {
        // Arrange
        var options = _defaultOptions;
        var mockNext = new Mock<RequestDelegate>();
        mockNext.Setup(next => next(It.IsAny<HttpContext>()))
               .ThrowsAsync(new InvalidOperationException("Test exception"));

        var middleware = new SecurityHeadersMiddleware(
            mockNext.Object,
            Options.Create(options),
            _mockLogger.Object);

        var context = CreateHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        // 驗證錯誤日誌
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error occurred in SecurityHeadersMiddleware")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithComplexCspConfiguration_ShouldGenerateCorrectCspString()
    {
        // Arrange
        var options = _defaultOptions;
        options.Csp = new ContentSecurityPolicyOptions
        {
            Enabled = true,
            DefaultSrc = "'none'",
            ScriptSrc = "'self' 'unsafe-inline' https://trusted.cdn.com",
            StyleSrc = "'self' 'unsafe-inline' fonts.googleapis.com",
            ImgSrc = "'self' data: https:",
            FontSrc = "'self' fonts.gstatic.com",
            ConnectSrc = "'self' api.example.com",
            ObjectSrc = "'none'",
            BaseUri = "'self'",
            FormAction = "'self'",
            FrameAncestors = "'none'",
            ReportUri = "/csp-report"
        };

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var cspValue = context.Response.Headers["Content-Security-Policy"].ToString();
        
        Assert.Contains("default-src 'none'", cspValue);
        Assert.Contains("script-src 'self' 'unsafe-inline' https://trusted.cdn.com", cspValue);
        Assert.Contains("style-src 'self' 'unsafe-inline' fonts.googleapis.com", cspValue);
        Assert.Contains("img-src 'self' data: https:", cspValue);
        Assert.Contains("font-src 'self' fonts.gstatic.com", cspValue);
        Assert.Contains("connect-src 'self' api.example.com", cspValue);
        Assert.Contains("object-src 'none'", cspValue);
        Assert.Contains("base-uri 'self'", cspValue);
        Assert.Contains("form-action 'self'", cspValue);
        Assert.Contains("frame-ancestors 'none'", cspValue);
        Assert.Contains("report-uri /csp-report", cspValue);
    }

    #region Helper Methods

    private SecurityHeadersMiddleware CreateMiddleware(SecurityHeadersOptions options)
    {
        return new SecurityHeadersMiddleware(
            _mockNext.Object,
            Options.Create(options),
            _mockLogger.Object);
    }

    private async Task<HttpContext> InvokeMiddlewareAndStartResponse(SecurityHeadersMiddleware middleware, HttpContext context)
    {
        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();
        return context;
    }

    private HttpContext CreateHttpContext(bool isHttps = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = isHttps ? "https" : "http";
        context.Request.Host = new HostString("localhost", 5001);
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    #endregion
}