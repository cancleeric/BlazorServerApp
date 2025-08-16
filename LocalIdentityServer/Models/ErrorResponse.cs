namespace LocalIdentityServer.Models;

/// <summary>
/// OAuth2/OIDC 標準錯誤回應格式 (RFC 6749)
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// 錯誤代碼 (required)
    /// 常見值: invalid_request, invalid_client, invalid_grant, unauthorized_client, 
    /// unsupported_grant_type, invalid_scope, server_error, temporarily_unavailable
    /// </summary>
    public required string Error { get; set; }

    /// <summary>
    /// 錯誤描述 (optional) - 人類可讀的錯誤說明
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// 錯誤詳細資訊 URI (optional) - 指向錯誤詳細說明的網頁
    /// </summary>
    public string? ErrorUri { get; set; }

    /// <summary>
    /// 狀態參數 (for authorization endpoint redirects)
    /// </summary>
    public string? State { get; set; }
}

/// <summary>
/// 授權端點錯誤類型 (RFC 6749 Section 4.1.2.1)
/// </summary>
public static class AuthorizationErrors
{
    public const string InvalidRequest = "invalid_request";
    public const string UnauthorizedClient = "unauthorized_client";
    public const string AccessDenied = "access_denied";
    public const string UnsupportedResponseType = "unsupported_response_type";
    public const string InvalidScope = "invalid_scope";
    public const string ServerError = "server_error";
    public const string TemporarilyUnavailable = "temporarily_unavailable";
}

/// <summary>
/// Token 端點錯誤類型 (RFC 6749 Section 5.2)
/// </summary>
public static class TokenErrors
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidClient = "invalid_client";
    public const string InvalidGrant = "invalid_grant";
    public const string UnauthorizedClient = "unauthorized_client";
    public const string UnsupportedGrantType = "unsupported_grant_type";
    public const string InvalidScope = "invalid_scope";
    public const string ServerError = "server_error";
}
