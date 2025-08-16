using LocalIdentityServer.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace LocalIdentityServer.Services;

/// <summary>
/// 錯誤處理與回應格式化服務
/// 負責統一處理 OAuth2/OIDC 錯誤回應格式
/// </summary>
public interface IErrorService
{
    /// <summary>
    /// 建立 Token 端點的 JSON 錯誤回應
    /// </summary>
    Task WriteTokenErrorAsync(HttpContext context, string error, string? description = null, int statusCode = 400);

    /// <summary>
    /// 建立授權端點的重導向錯誤 (回到 client redirect_uri)
    /// </summary>
    string BuildAuthorizationErrorRedirect(string redirectUri, string error, string? description = null, string? state = null);

    /// <summary>
    /// 建立錯誤顯示頁面 URL (/error)
    /// </summary>
    string BuildErrorPageUrl(string error, string? description = null);

    /// <summary>
    /// 建立 UserInfo 端點的錯誤回應
    /// </summary>
    Task WriteUserInfoErrorAsync(HttpContext context, string error, string? description = null, int statusCode = 400);

    /// <summary>
    /// 建立一般性錯誤回應
    /// </summary>
    Task WriteGenericErrorAsync(HttpContext context, string error, string? description = null, int statusCode = 500);
}

public class DefaultErrorService : IErrorService
{
    public async Task WriteTokenErrorAsync(HttpContext context, string error, string? description = null, int statusCode = 400)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrEmpty(error)) throw new ArgumentException("Error cannot be null or empty", nameof(error));

        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var errorResponse = new ErrorResponse
        {
            Error = error,
            ErrorDescription = description
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }

    public string BuildAuthorizationErrorRedirect(string redirectUri, string error, string? description = null, string? state = null)
    {
        if (string.IsNullOrEmpty(redirectUri)) throw new ArgumentException("Redirect URI cannot be null or empty", nameof(redirectUri));
        if (string.IsNullOrEmpty(error)) throw new ArgumentException("Error cannot be null or empty", nameof(error));

        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error
        };

        if (!string.IsNullOrEmpty(description))
            parameters["error_description"] = description;

        if (!string.IsNullOrEmpty(state))
            parameters["state"] = state;

        return QueryHelpers.AddQueryString(redirectUri, parameters);
    }

    public string BuildErrorPageUrl(string error, string? description = null)
    {
        if (string.IsNullOrEmpty(error)) throw new ArgumentException("Error cannot be null or empty", nameof(error));

        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error
        };

        if (!string.IsNullOrEmpty(description))
            parameters["error_description"] = description;

        return QueryHelpers.AddQueryString("/error", parameters);
    }

    public async Task WriteUserInfoErrorAsync(HttpContext context, string error, string? description = null, int statusCode = 400)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrEmpty(error)) throw new ArgumentException("Error cannot be null or empty", nameof(error));

        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var errorResponse = new ErrorResponse
        {
            Error = error,
            ErrorDescription = description
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }

    public async Task WriteGenericErrorAsync(HttpContext context, string error, string? description = null, int statusCode = 500)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrEmpty(error)) throw new ArgumentException("Error cannot be null or empty", nameof(error));

        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var errorResponse = new ErrorResponse
        {
            Error = error,
            ErrorDescription = description
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }
}
