using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using LocalIdentityServer.Models;
using LocalIdentityServer.Services;
using LocalIdentityServer.Stores;
using System.IdentityModel.Tokens.Jwt;

namespace LocalIdentityServer.Endpoints;

/// <summary>
/// OAuth2/OIDC 授權端點 - 遵循單一責任原則 (SRP)
/// 負責授權碼流程和權杖發行
/// </summary>
public static class AuthorizationEndpoints
{
    /// <summary>
    /// 配置授權相關端點
    /// </summary>
    public static void MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // OAuth2 授權端點
        endpoints.MapGet("/connect/authorize", ProcessAuthorization)
            .WithName("ProcessAuthorization")
            .WithDisplayName("OAuth2 Authorization Endpoint")
            .Produces(302)
            .Produces(400)
            .WithTags("OAuth2");

        // OAuth2 權杖端點
        endpoints.MapPost("/connect/token", ProcessTokenRequest)
            .WithName("ProcessTokenRequest")
            .WithDisplayName("OAuth2 Token Endpoint")
            .Accepts<TokenRequest>("application/x-www-form-urlencoded")
            .Produces(200)
            .Produces(400)
            .Produces(401)
            .WithTags("OAuth2");
    }

    /// <summary>
    /// 處理授權請求 (Authorization Code Flow)
    /// </summary>
    private static async Task<IResult> ProcessAuthorization(
        HttpContext context,
        IAuthorizationCodeStore codeStore,
        IErrorService errorService,
        [FromServices] IList<TestClient> clients)
    {
        try
        {
            // 提取請求參數
            var request = ExtractAuthorizationRequest(context);

            // 基本參數驗證 - 遵循防禦性編程
            var validationResult = ValidateAuthorizationRequest(request, clients, errorService);
            if (validationResult != null)
            {
                context.Response.Redirect(validationResult);
                return Results.Empty;
            }

            // 檢查使用者登入狀態
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                var returnUrl = context.Request.Path + context.Request.QueryString;
                context.Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return Results.Empty;
            }

            // 產生授權碼
            var authCode = await GenerateAuthorizationCode(context, request, codeStore);

            // 重導向回客戶端
            var location = BuildAuthorizationResponse(request, authCode);
            context.Response.Redirect(location);

            return Results.Empty;
        }
        catch (Exception ex)
        {
            // 記錄錯誤並重導向到錯誤頁面
            var errorUrl = errorService.BuildErrorPageUrl(
                AuthorizationErrors.ServerError, 
                "An unexpected error occurred");
            context.Response.Redirect(errorUrl);
            return Results.Empty;
        }
    }

    /// <summary>
    /// 處理權杖請求
    /// </summary>
    private static async Task<IResult> ProcessTokenRequest(
        HttpContext context,
        IAuthorizationCodeStore codeStore,
        IRefreshTokenStore refreshStore,
        ITokenService tokenService,
        IErrorService errorService,
        [FromServices] IList<TestUser> users,
        [FromServices] IList<TestClient> clients,
        SigningCredentials signingCredentials)
    {
        try
        {
            if (!context.Request.HasFormContentType)
            {
                await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidRequest, 
                    "Request must be application/x-www-form-urlencoded");
                return Results.Empty;
            }

            var form = await context.Request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();

            // 基本驗證
            if (string.IsNullOrEmpty(grantType))
            {
                await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidRequest, 
                    "Missing grant_type parameter");
                return Results.Empty;
            }

            // 客戶端驗證
            var client = await ValidateClient(form, clients, errorService, context);
            if (client == null) return Results.Empty;

            // 根據授權類型處理
            return grantType switch
            {
                "authorization_code" => await ProcessAuthorizationCodeGrant(
                    context, form, codeStore, tokenService, users, client, errorService),
                "refresh_token" => await ProcessRefreshTokenGrant(
                    context, form, refreshStore, tokenService, users, client, errorService),
                "client_credentials" => await ProcessClientCredentialsGrant(
                    context, form, client, signingCredentials),
                "password" => await ProcessPasswordGrant(
                    context, form, tokenService, users, client, errorService),
                _ => await HandleUnsupportedGrantType(context, grantType, errorService)
            };
        }
        catch (Exception ex)
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.ServerError, 
                "An unexpected error occurred");
            return Results.Empty;
        }
    }

    #region Helper Methods

    /// <summary>
    /// 提取授權請求參數
    /// </summary>
    private static AuthorizationRequest ExtractAuthorizationRequest(HttpContext context)
    {
        var query = context.Request.Query;
        return new AuthorizationRequest(
            query["response_type"].ToString(),
            query["client_id"].ToString(),
            query["redirect_uri"].ToString(),
            query["scope"].ToString(),
            query["state"].ToString(),
            query["code_challenge"].ToString(),
            query["code_challenge_method"].ToString(),
            query["nonce"].ToString()
        );
    }

    /// <summary>
    /// 驗證授權請求
    /// </summary>
    private static string? ValidateAuthorizationRequest(
        AuthorizationRequest request,
        [FromServices] IList<TestClient> clients,
        IErrorService errorService)
    {
        // 必要參數檢查
        if (string.IsNullOrEmpty(request.ResponseType) || 
            string.IsNullOrEmpty(request.ClientId) || 
            string.IsNullOrEmpty(request.RedirectUri))
        {
            return errorService.BuildErrorPageUrl(AuthorizationErrors.InvalidRequest,
                "Missing required parameters: response_type, client_id, or redirect_uri");
        }

        // 回應類型檢查
        if (request.ResponseType != "code")
        {
            return errorService.BuildErrorPageUrl(AuthorizationErrors.UnsupportedResponseType,
                "Only authorization code flow is supported");
        }

        // 客戶端驗證
        var client = clients.FirstOrDefault(c => c.ClientId == request.ClientId);
        if (client == null)
        {
            return errorService.BuildErrorPageUrl(AuthorizationErrors.UnauthorizedClient,
                "Invalid client_id");
        }

        // 重導向 URI 驗證
        if (!string.Equals(client.RedirectUri, request.RedirectUri, StringComparison.OrdinalIgnoreCase))
        {
            return errorService.BuildErrorPageUrl(AuthorizationErrors.InvalidRequest,
                "Redirect URI mismatch");
        }

        return null; // 驗證通過
    }

    /// <summary>
    /// 產生授權碼
    /// </summary>
    private static async Task<string> GenerateAuthorizationCode(
        HttpContext context,
        AuthorizationRequest request,
        IAuthorizationCodeStore codeStore)
    {
        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        
        await codeStore.StoreAsync(new AuthorizationCode
        {
            Code = code,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope,
            Subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Username = context.User.FindFirstValue("username")!,
            Email = context.User.FindFirstValue(ClaimTypes.Email)!,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            Nonce = request.Nonce,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });

        return code;
    }

    /// <summary>
    /// 建立授權回應
    /// </summary>
    private static string BuildAuthorizationResponse(AuthorizationRequest request, string code)
    {
        var location = $"{request.RedirectUri}?code={code}";
        if (!string.IsNullOrEmpty(request.State))
        {
            location += $"&state={Uri.EscapeDataString(request.State)}";
        }
        return location;
    }

    /// <summary>
    /// 驗證客戶端
    /// </summary>
    private static async Task<TestClient?> ValidateClient(
        IFormCollection form,
        [FromServices] IList<TestClient> clients,
        IErrorService errorService,
        HttpContext context)
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();

        var client = clients.FirstOrDefault(c => c.ClientId == clientId && c.ClientSecret == clientSecret);
        if (client == null)
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidClient,
                "Invalid client credentials");
        }

        return client;
    }

    /// <summary>
    /// 處理授權碼授權類型
    /// </summary>
    private static async Task<IResult> ProcessAuthorizationCodeGrant(
        HttpContext context,
        IFormCollection form,
        IAuthorizationCodeStore codeStore,
        ITokenService tokenService,
        [FromServices] IList<TestUser> users,
        TestClient client,
        IErrorService errorService)
    {
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();

        if (string.IsNullOrEmpty(code))
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidRequest,
                "Missing authorization code");
            return Results.Empty;
        }

        var codeObj = await codeStore.TakeAsync(code);
        if (codeObj == null)
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidGrant,
                "Invalid or expired authorization code");
            return Results.Empty;
        }

        if (codeObj.ExpiresAt < DateTime.UtcNow || 
            codeObj.ClientId != client.ClientId || 
            codeObj.RedirectUri != redirectUri)
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidGrant,
                "Authorization code validation failed");
            return Results.Empty;
        }

        // PKCE 驗證
        if (!await ValidatePkce(form, codeObj, errorService, context))
            return Results.Empty;

        var user = users.First(u => u.Id == codeObj.Subject);
        return await IssueTokensForUser(context, user, client.ClientId, codeObj.Scope, 
            tokenService, codeObj.Nonce);
    }

    /// <summary>
    /// 處理其他授權類型的方法...
    /// </summary>
    private static async Task<IResult> ProcessRefreshTokenGrant(
        HttpContext context,
        IFormCollection form,
        IRefreshTokenStore refreshStore,
        ITokenService tokenService,
        [FromServices] IList<TestUser> users,
        TestClient client,
        IErrorService errorService)
    {
        // 實作細節...
        await errorService.WriteTokenErrorAsync(context, TokenErrors.UnsupportedGrantType,
            "Refresh token grant not yet implemented with new architecture");
        return Results.Empty;
    }

    private static async Task<IResult> ProcessClientCredentialsGrant(
        HttpContext context,
        IFormCollection form,
        TestClient client,
        SigningCredentials signingCredentials)
    {
        var scope = form["scope"].ToString();
        var now = DateTime.UtcNow;
        
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new("scope", scope)
        };

        var access = new JwtSecurityToken(
            issuer: "https://localhost:5055",
            audience: "api_resource_a",
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(10),
            signingCredentials: signingCredentials
        );

        var handler = new JwtSecurityTokenHandler();
        await context.Response.WriteAsJsonAsync(new
        {
            access_token = handler.WriteToken(access),
            token_type = "Bearer",
            expires_in = 600,
            scope = scope
        });

        return Results.Empty;
    }

    private static async Task<IResult> ProcessPasswordGrant(
        HttpContext context,
        IFormCollection form,
        ITokenService tokenService,
        [FromServices] IList<TestUser> users,
        TestClient client,
        IErrorService errorService)
    {
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var scope = form["scope"].ToString();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidRequest,
                "Missing username or password");
            return Results.Empty;
        }

        var user = users.FirstOrDefault(u => u.UserName == username && u.Password == password);
        if (user == null)
        {
            await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidGrant,
                "Invalid username or password");
            return Results.Empty;
        }

        return await IssueTokensForUser(context, user, client.ClientId, scope, tokenService);
    }

    private static async Task<IResult> HandleUnsupportedGrantType(
        HttpContext context,
        string grantType,
        IErrorService errorService)
    {
        await errorService.WriteTokenErrorAsync(context, TokenErrors.UnsupportedGrantType,
            $"Grant type '{grantType}' is not supported");
        return Results.Empty;
    }

    /// <summary>
    /// PKCE 驗證
    /// </summary>
    private static async Task<bool> ValidatePkce(
        IFormCollection form,
        AuthorizationCode codeObj,
        IErrorService errorService,
        HttpContext context)
    {
        var codeVerifier = form["code_verifier"].ToString();
        
        if (!string.IsNullOrEmpty(codeObj.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
            {
                await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidRequest,
                    "Missing code_verifier for PKCE");
                return false;
            }

            string derived = codeObj.CodeChallengeMethod == "S256"
                ? Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)))
                : codeVerifier; // plain

            if (derived != codeObj.CodeChallenge)
            {
                await errorService.WriteTokenErrorAsync(context, TokenErrors.InvalidGrant,
                    "PKCE validation failed");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 為使用者發行權杖
    /// </summary>
    private static async Task<IResult> IssueTokensForUser(
        HttpContext context,
        TestUser user,
        string clientId,
        string scope,
        ITokenService tokenService,
        string? nonce = null)
    {
        var result = await tokenService.IssueAsync(new TokenIssueRequest
        {
            User = user,
            ClientId = clientId,
            Scope = scope,
            Nonce = nonce
        });

        await context.Response.WriteAsJsonAsync(new
        {
            access_token = result.AccessToken,
            token_type = "Bearer",
            expires_in = result.ExpiresIn,
            scope = result.Scope,
            id_token = result.IdToken,
            refresh_token = result.RefreshToken?.Token
        });

        return Results.Empty;
    }

    #endregion

    /// <summary>
    /// 授權請求參數模型
    /// </summary>
    private record AuthorizationRequest(
        string ResponseType,
        string ClientId,
        string RedirectUri,
        string Scope,
        string State,
        string CodeChallenge,
        string CodeChallengeMethod,
        string Nonce);

    /// <summary>
    /// 權杖請求模型
    /// </summary>
    private record TokenRequest(
        string GrantType,
        string ClientId,
        string ClientSecret,
        string? Code,
        string? RedirectUri,
        string? CodeVerifier,
        string? RefreshToken,
        string? Username,
        string? Password,
        string? Scope);
}
