namespace LocalIdentityServer.Models;

/// <summary>
/// 安全標頭配置選項 - 遵循 SOLID 原則
/// 包含 HTTPS 強化、HSTS、CSP 與完整安全標頭設定
/// </summary>
public class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    /// <summary>
    /// 是否啟用安全標頭
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// HTTPS 強制重導向設定
    /// </summary>
    public HttpsRedirectionOptions HttpsRedirection { get; set; } = new();

    /// <summary>
    /// HSTS (HTTP Strict Transport Security) 設定
    /// </summary>
    public HstsOptions Hsts { get; set; } = new();

    /// <summary>
    /// Content Security Policy 設定
    /// </summary>
    public ContentSecurityPolicyOptions Csp { get; set; } = new();

    /// <summary>
    /// 框架保護設定 (X-Frame-Options)
    /// </summary>
    public FrameOptions FrameOptions { get; set; } = new();

    /// <summary>
    /// 內容類型選項 (X-Content-Type-Options)
    /// </summary>
    public ContentTypeOptions ContentTypeOptions { get; set; } = new();

    /// <summary>
    /// 引用者政策 (Referrer-Policy)
    /// </summary>
    public ReferrerPolicyOptions ReferrerPolicy { get; set; } = new();

    /// <summary>
    /// 跨域嵌入程式政策 (Cross-Origin-Embedder-Policy)
    /// </summary>
    public CrossOriginEmbedderPolicyOptions CrossOriginEmbedderPolicy { get; set; } = new();

    /// <summary>
    /// 跨域開放政策 (Cross-Origin-Opener-Policy)
    /// </summary>
    public CrossOriginOpenerPolicyOptions CrossOriginOpenerPolicy { get; set; } = new();

    /// <summary>
    /// 跨域資源政策 (Cross-Origin-Resource-Policy)
    /// </summary>
    public CrossOriginResourcePolicyOptions CrossOriginResourcePolicy { get; set; } = new();

    /// <summary>
    /// 權限政策 (Permissions-Policy)
    /// </summary>
    public PermissionsPolicyOptions PermissionsPolicy { get; set; } = new();

    /// <summary>
    /// 自訂安全標頭
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// 要移除的不安全標頭
    /// </summary>
    public List<string> RemoveHeaders { get; set; } = new()
    {
        "Server",
        "X-Powered-By",
        "X-AspNet-Version",
        "X-AspNetMvc-Version"
    };
}

/// <summary>
/// HTTPS 重導向設定
/// </summary>
public class HttpsRedirectionOptions
{
    /// <summary>
    /// 是否啟用 HTTPS 強制重導向
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// HTTPS 重導向埠號
    /// </summary>
    public int? RedirectStatusCode { get; set; } = 308; // Permanent Redirect

    /// <summary>
    /// HTTPS 埠號
    /// </summary>
    public int? HttpsPort { get; set; }
}

/// <summary>
/// HSTS (HTTP Strict Transport Security) 設定
/// </summary>
public class HstsOptions
{
    /// <summary>
    /// 是否啟用 HSTS
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// HSTS 最大期限 (秒) - 預設 1 年
    /// </summary>
    public long MaxAge { get; set; } = 31536000; // 1 year

    /// <summary>
    /// 是否包含子網域
    /// </summary>
    public bool IncludeSubDomains { get; set; } = true;

    /// <summary>
    /// 是否預載入 (僅在正式環境使用)
    /// </summary>
    public bool Preload { get; set; } = false;
}

/// <summary>
/// Content Security Policy 設定
/// </summary>
public class ContentSecurityPolicyOptions
{
    /// <summary>
    /// 是否啟用 CSP
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否使用 Report-Only 模式 (測試用)
    /// </summary>
    public bool ReportOnly { get; set; } = false;

    /// <summary>
    /// 預設來源
    /// </summary>
    public string DefaultSrc { get; set; } = "'self'";

    /// <summary>
    /// 腳本來源
    /// </summary>
    public string ScriptSrc { get; set; } = "'self' 'unsafe-inline'";

    /// <summary>
    /// 樣式來源
    /// </summary>
    public string StyleSrc { get; set; } = "'self' 'unsafe-inline'";

    /// <summary>
    /// 圖片來源
    /// </summary>
    public string ImgSrc { get; set; } = "'self' data:";

    /// <summary>
    /// 字型來源
    /// </summary>
    public string FontSrc { get; set; } = "'self'";

    /// <summary>
    /// 連接來源 (AJAX, WebSocket 等)
    /// </summary>
    public string ConnectSrc { get; set; } = "'self'";

    /// <summary>
    /// 媒體來源
    /// </summary>
    public string MediaSrc { get; set; } = "'self'";

    /// <summary>
    /// 物件來源 (Flash 等)
    /// </summary>
    public string ObjectSrc { get; set; } = "'none'";

    /// <summary>
    /// 子框架來源
    /// </summary>
    public string ChildSrc { get; set; } = "'self'";

    /// <summary>
    /// 框架祖先來源
    /// </summary>
    public string FrameAncestors { get; set; } = "'none'";

    /// <summary>
    /// 基礎 URI
    /// </summary>
    public string BaseUri { get; set; } = "'self'";

    /// <summary>
    /// 表單動作來源
    /// </summary>
    public string FormAction { get; set; } = "'self'";

    /// <summary>
    /// CSP 違規報告端點
    /// </summary>
    public string? ReportUri { get; set; }

    /// <summary>
    /// 自訂 CSP 指令
    /// </summary>
    public Dictionary<string, string> CustomDirectives { get; set; } = new();
}

/// <summary>
/// X-Frame-Options 設定
/// </summary>
public class FrameOptions
{
    /// <summary>
    /// 是否啟用 X-Frame-Options
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 框架選項：DENY, SAMEORIGIN, ALLOW-FROM
    /// </summary>
    public string Policy { get; set; } = "DENY";

    /// <summary>
    /// 允許的來源 (當 Policy 為 ALLOW-FROM 時使用)
    /// </summary>
    public string? AllowFrom { get; set; }
}

/// <summary>
/// X-Content-Type-Options 設定
/// </summary>
public class ContentTypeOptions
{
    /// <summary>
    /// 是否啟用 X-Content-Type-Options
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 內容類型選項：nosniff
    /// </summary>
    public string Value { get; set; } = "nosniff";
}

/// <summary>
/// Referrer-Policy 設定
/// </summary>
public class ReferrerPolicyOptions
{
    /// <summary>
    /// 是否啟用 Referrer-Policy
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 引用者政策值
    /// </summary>
    public string Policy { get; set; } = "strict-origin-when-cross-origin";
}

/// <summary>
/// Cross-Origin-Embedder-Policy 設定
/// </summary>
public class CrossOriginEmbedderPolicyOptions
{
    /// <summary>
    /// 是否啟用 COEP
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// COEP 政策值
    /// </summary>
    public string Policy { get; set; } = "require-corp";
}

/// <summary>
/// Cross-Origin-Opener-Policy 設定
/// </summary>
public class CrossOriginOpenerPolicyOptions
{
    /// <summary>
    /// 是否啟用 COOP
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// COOP 政策值
    /// </summary>
    public string Policy { get; set; } = "same-origin";
}

/// <summary>
/// Cross-Origin-Resource-Policy 設定
/// </summary>
public class CrossOriginResourcePolicyOptions
{
    /// <summary>
    /// 是否啟用 CORP
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// CORP 政策值
    /// </summary>
    public string Policy { get; set; } = "same-origin";
}

/// <summary>
/// Permissions-Policy 設定
/// </summary>
public class PermissionsPolicyOptions
{
    /// <summary>
    /// 是否啟用 Permissions-Policy
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 政策設定 (功能名稱 -> 允許清單)
    /// </summary>
    public Dictionary<string, string> Policies { get; set; } = new()
    {
        ["geolocation"] = "()",
        ["microphone"] = "()",
        ["camera"] = "()",
        ["fullscreen"] = "(self)",
        ["payment"] = "()"
    };
}