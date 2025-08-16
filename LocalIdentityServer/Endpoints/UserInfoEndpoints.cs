using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using LocalIdentityServer.Models;
using LocalIdentityServer.Services;

namespace LocalIdentityServer.Endpoints;

/// <summary>
/// 使用者資訊端點 - 遵循單一責任原則 (SRP)
/// 負責提供 OIDC UserInfo 端點功能
/// </summary>
public static class UserInfoEndpoints
{
    /// <summary>
    /// 配置使用者資訊相關端點
    /// </summary>
    public static void MapUserInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // OIDC UserInfo 端點
        endpoints.MapGet("/connect/userinfo", GetUserInfo)
            .WithName("GetUserInfo")
            .WithDisplayName("OIDC UserInfo Endpoint")
            .RequireAuthorization()
            .Produces(200)
            .Produces(401)
            .Produces(403)
            .WithTags("OIDC");

        // 登出端點
        endpoints.MapPost("/logout", ProcessLogout)
            .WithName("ProcessLogout")
            .WithDisplayName("User Logout Endpoint")
            .RequireAuthorization()
            .Produces(200)
            .Produces(302)
            .WithTags("Authentication");

        // 用戶資訊管理端點
        endpoints.MapGet("/account/profile", GetUserProfile)
            .WithName("GetUserProfile")
            .WithDisplayName("User Profile Information")
            .RequireAuthorization()
            .Produces(200)
            .Produces(401)
            .WithTags("User Management");
    }

    /// <summary>
    /// OIDC UserInfo 端點實作
    /// 根據 OpenID Connect Core 規範提供使用者資訊
    /// </summary>
    private static async Task<IResult> GetUserInfo(
        HttpContext context,
        [FromServices] IList<TestUser> users,
        IErrorService errorService,
        ILogger<Program> logger)
    {
        try
        {
            // 檢查使用者身份驗證狀態
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await errorService.WriteUserInfoErrorAsync(context, "invalid_token", 
                    "The access token provided is expired, revoked, malformed, or invalid");
                return Results.Unauthorized();
            }

            // 從 Token 中取得使用者識別碼
            var subject = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(subject))
            {
                logger.LogWarning("UserInfo request missing subject claim");
                await errorService.WriteUserInfoErrorAsync(context, "invalid_token", 
                    "Token does not contain valid subject information");
                return Results.Unauthorized();
            }

            // 查找使用者
            var user = users.FirstOrDefault(u => u.Id == subject);
            if (user == null)
            {
                logger.LogWarning("UserInfo request for non-existent user: {Subject}", subject);
                await errorService.WriteUserInfoErrorAsync(context, "invalid_token", 
                    "User not found");
                return Results.Unauthorized();
            }

            // 檢查 scope 權限
            var scopes = GetAuthorizedScopes(context);
            var userInfo = BuildUserInfoResponse(user, scopes);

            // 記錄存取日誌
            logger.LogInformation("UserInfo accessed for user: {Username}, scopes: {Scopes}", 
                user.UserName, string.Join(",", scopes));

            return Results.Json(userInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing UserInfo request");
            await errorService.WriteUserInfoErrorAsync(context, "server_error", 
                "An internal error occurred");
            return Results.Problem("Internal server error", statusCode: 500);
        }
    }

    /// <summary>
    /// 處理使用者登出
    /// </summary>
    private static async Task<IResult> ProcessLogout(
        HttpContext context,
        IErrorService errorService,
        ILogger<Program> logger)
    {
        try
        {
            var userName = context.User.Identity?.Name;
            
            // 執行登出 - 清除 Cookie 驗證
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // 如果有 Bearer Token，加入撤銷邏輯
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                // TODO: 實作 Token 撤銷邏輯
                logger.LogInformation("Token revocation requested for user: {UserName}", userName);
            }

            // 檢查是否有 post_logout_redirect_uri
            var redirectUri = context.Request.Query["post_logout_redirect_uri"].ToString();
            var state = context.Request.Query["state"].ToString();

            if (!string.IsNullOrEmpty(redirectUri))
            {
                // 驗證重導向 URI (簡化版本，生產環境需要更嚴格驗證)
                if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
                {
                    var location = redirectUri;
                    if (!string.IsNullOrEmpty(state))
                    {
                        location += (redirectUri.Contains('?') ? "&" : "?") + $"state={Uri.EscapeDataString(state)}";
                    }
                    
                    logger.LogInformation("User {UserName} logged out, redirecting to: {RedirectUri}", 
                        userName, redirectUri);
                    
                    return Results.Redirect(location);
                }
            }

            logger.LogInformation("User {UserName} logged out successfully", userName);
            
            // 返回登出成功回應
            return Results.Json(new
            {
                message = "Logout successful",
                timestamp = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing logout request");
            await errorService.WriteGenericErrorAsync(context, "server_error", 
                "An error occurred during logout");
            return Results.Problem("Internal server error", statusCode: 500);
        }
    }

    /// <summary>
    /// 取得使用者資料檔案
    /// </summary>
    private static async Task<IResult> GetUserProfile(
        HttpContext context,
        [FromServices] IList<TestUser> users,
        IErrorService errorService,
        ILogger<Program> logger)
    {
        try
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Results.Unauthorized();
            }

            var subject = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(subject))
            {
                return Results.Unauthorized();
            }

            var user = users.FirstOrDefault(u => u.Id == subject);
            if (user == null)
            {
                return Results.NotFound();
            }

            // 建立詳細的使用者資料檔案
            var profile = new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.Department,
                user.JobTitle,
                Claims = user.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                LastLogin = context.User.FindFirstValue("auth_time"),
                ProfileCompleteness = CalculateProfileCompleteness(user),
                AccountStatus = "Active",
                CreatedAt = user.CreatedAt?.ToString("O"),
                UpdatedAt = DateTime.UtcNow.ToString("O")
            };

            logger.LogInformation("Profile accessed for user: {Username}", user.UserName);
            
            return Results.Json(profile);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user profile");
            await errorService.WriteGenericErrorAsync(context, "server_error", 
                "An error occurred while retrieving profile");
            return Results.Problem("Internal server error", statusCode: 500);
        }
    }

    #region Helper Methods

    /// <summary>
    /// 取得授權的 Scopes
    /// </summary>
    private static List<string> GetAuthorizedScopes(HttpContext context)
    {
        var scopeClaim = context.User.FindFirstValue("scope");
        if (string.IsNullOrEmpty(scopeClaim))
        {
            return new List<string> { "openid" }; // 預設至少包含 openid
        }

        return scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// 根據 Scopes 建立 UserInfo 回應
    /// 遵循 OpenID Connect Core 規範
    /// </summary>
    private static object BuildUserInfoResponse(TestUser user, List<string> scopes)
    {
        var userInfo = new Dictionary<string, object>
        {
            ["sub"] = user.Id
        };

        // profile scope
        if (scopes.Contains("profile"))
        {
            AddIfNotEmpty(userInfo, "name", user.FullName);
            AddIfNotEmpty(userInfo, "given_name", user.GivenName);
            AddIfNotEmpty(userInfo, "family_name", user.FamilyName);
            AddIfNotEmpty(userInfo, "preferred_username", user.UserName);
            AddIfNotEmpty(userInfo, "picture", user.Picture);
            AddIfNotEmpty(userInfo, "website", user.Website);
            AddIfNotEmpty(userInfo, "gender", user.Gender);
            AddIfNotEmpty(userInfo, "birthdate", user.BirthDate);
            AddIfNotEmpty(userInfo, "zoneinfo", user.ZoneInfo);
            AddIfNotEmpty(userInfo, "locale", user.Locale);
            
            if (user.UpdatedAt.HasValue)
            {
                userInfo["updated_at"] = ((DateTimeOffset)user.UpdatedAt.Value).ToUnixTimeSeconds();
            }
        }

        // email scope
        if (scopes.Contains("email"))
        {
            AddIfNotEmpty(userInfo, "email", user.Email);
            userInfo["email_verified"] = user.EmailVerified;
        }

        // phone scope
        if (scopes.Contains("phone"))
        {
            AddIfNotEmpty(userInfo, "phone_number", user.PhoneNumber);
            userInfo["phone_number_verified"] = user.PhoneNumberVerified;
        }

        // address scope
        if (scopes.Contains("address") && user.Address != null)
        {
            userInfo["address"] = new
            {
                formatted = user.Address.Formatted,
                street_address = user.Address.StreetAddress,
                locality = user.Address.Locality,
                region = user.Address.Region,
                postal_code = user.Address.PostalCode,
                country = user.Address.Country
            };
        }

        // 自定義 claims (如果在 scope 中)
        if (scopes.Contains("roles") && user.Claims.Any(c => c.Type == "role"))
        {
            userInfo["roles"] = user.Claims
                .Where(c => c.Type == "role")
                .Select(c => c.Value)
                .ToArray();
        }

        if (scopes.Contains("department"))
        {
            AddIfNotEmpty(userInfo, "department", user.Department);
            AddIfNotEmpty(userInfo, "job_title", user.JobTitle);
        }

        return userInfo;
    }

    /// <summary>
    /// 輔助方法：僅在值不為空時加入字典
    /// </summary>
    private static void AddIfNotEmpty(Dictionary<string, object> dict, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            dict[key] = value;
        }
    }

    /// <summary>
    /// 計算資料檔案完整度
    /// </summary>
    private static int CalculateProfileCompleteness(TestUser user)
    {
        var fields = new[]
        {
            user.FullName, user.Email, user.PhoneNumber, 
            user.Department, user.JobTitle, user.Picture
        };

        var completedFields = fields.Count(f => !string.IsNullOrEmpty(f));
        return (int)Math.Round((double)completedFields / fields.Length * 100);
    }

    #endregion
}
