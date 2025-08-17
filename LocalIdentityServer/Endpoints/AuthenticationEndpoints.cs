using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LocalIdentityServer.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        // Token endpoint
        group.MapPost("/token", HandleTokenRequest);
    }

    private static async Task<IResult> HandleTokenRequest(
        HttpContext context,
        IUserRepository userRepository,
        IClientRepository clientRepository,
        IAuthorizationCodeRepository authCodeRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenRotationService refreshTokenRotationService,
        IPersistedKeyRepository keyRepository,
        IErrorService errorService,
        IConfiguration configuration,
        LocalIdentityServer.Services.MFA.IMfaService mfaService)
    {
        if (!context.Request.HasFormContentType)
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_request", 
                "Content-Type must be application/x-www-form-urlencoded");
            return Results.BadRequest();
        }

        var form = await context.Request.ReadFormAsync();
        var grantType = form["grant_type"].ToString();

        return grantType switch
        {
            "authorization_code" => await HandleAuthorizationCodeGrant(form, context, userRepository, 
                clientRepository, authCodeRepository, refreshTokenRepository, keyRepository, 
                errorService, configuration, mfaService),
            
            "refresh_token" => await HandleRefreshTokenGrant(form, userRepository, 
                clientRepository, refreshTokenRepository, refreshTokenRotationService, 
                keyRepository, errorService, configuration),
            
            "client_credentials" => await HandleClientCredentialsGrant(form, clientRepository, 
                keyRepository, errorService, configuration),
                
            "urn:ietf:params:oauth:grant-type:mfa" => await HandleMfaGrant(form, context, 
                userRepository, clientRepository, keyRepository, errorService, configuration, mfaService),
            
            _ => await Task.Run(async () => {
                await errorService.WriteTokenErrorAsync(context, "unsupported_grant_type", 
                    $"Grant type '{grantType}' is not supported");
                return Results.BadRequest();
            })
        };
    }

    private static async Task<IResult> HandleAuthorizationCodeGrant(
        IFormCollection form,
        HttpContext context,
        IUserRepository userRepository,
        IClientRepository clientRepository,
        IAuthorizationCodeRepository authCodeRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPersistedKeyRepository keyRepository,
        IErrorService errorService,
        IConfiguration configuration,
        LocalIdentityServer.Services.MFA.IMfaService mfaService)
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(code) || 
            string.IsNullOrEmpty(redirectUri))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_request", 
                "缺少必要參數");
            return Results.BadRequest();
        }

        // 驗證客戶端
        var client = await clientRepository.GetByClientIdAsync(clientId);
        if (client == null || !client.IsActive)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_client", 
                "無效的客戶端");
            return Results.Unauthorized();
        }

        if (!string.IsNullOrEmpty(clientSecret) && 
            !BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_client", 
                "客戶端認證失敗");
            return Results.Unauthorized();
        }

        // 驗證授權碼
        var authCode = await authCodeRepository.GetByCodeAsync(code);
        if (authCode == null || authCode.IsUsed || authCode.ExpiresAt <= DateTime.UtcNow)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                "授權碼無效或已過期");
            return Results.BadRequest();
        }

        if (authCode.ClientId != clientId || authCode.RedirectUri != redirectUri)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                "授權碼與客戶端或重導向URI不匹配");
            return Results.BadRequest();
        }

        // PKCE 驗證
        if (!string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
            {
                await errorService.WriteTokenErrorAsync(null!, "invalid_request", 
                    "缺少code_verifier");
                return Results.BadRequest();
            }

            var computedChallenge = authCode.CodeChallengeMethod == "S256" 
                ? Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(codeVerifier)))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_')
                : codeVerifier;

            if (computedChallenge != authCode.CodeChallenge)
            {
                await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                    "PKCE驗證失敗");
                return Results.BadRequest();
            }
        }

        // 標記授權碼已使用
        authCode.IsUsed = true;
        authCode.UsedAt = DateTime.UtcNow;
        await authCodeRepository.StoreAsync(authCode);

        // 產生Token
        var user = await userRepository.GetByIdAsync(authCode.Subject);
        if (user == null)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                "用戶不存在");
            return Results.BadRequest();
        }

        // 檢查是否需要 MFA 驗證
        if (await mfaService.IsMfaEnabledAsync(user.Id) && 
            await mfaService.RequiresMfaVerificationAsync(user.Id))
        {
            // 檢查是否已完成 MFA 驗證 (透過 auth_code 中的 MFA 標記)
            if (string.IsNullOrEmpty(authCode.MfaVerified) || authCode.MfaVerified != "true")
            {
                // 返回需要 MFA 驗證的錯誤
                await errorService.WriteTokenErrorAsync(context, "mfa_required", 
                    "Multi-factor authentication is required");
                return Results.BadRequest(new
                {
                    error = "mfa_required",
                    error_description = "Multi-factor authentication is required",
                    mfa_token = authCode.Code // 使用原始 auth code 作為 MFA token
                });
            }
        }

        var tokens = await GenerateTokensAsync(user, client, authCode.Scope.Split(' '), 
            keyRepository, refreshTokenRepository, configuration, authCode.Nonce);

        return Results.Ok(tokens);
    }

    private static async Task<IResult> HandleRefreshTokenGrant(
        IFormCollection form,
        IUserRepository userRepository,
        IClientRepository clientRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenRotationService refreshTokenRotationService,
        IPersistedKeyRepository keyRepository,
        IErrorService errorService,
        IConfiguration configuration)
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var refreshToken = form["refresh_token"].ToString();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(refreshToken))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_request", 
                "缺少必要參數");
            return Results.BadRequest();
        }

        // 驗證客戶端
        var client = await clientRepository.GetByClientIdAsync(clientId);
        if (client == null || !client.IsActive)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_client", 
                "無效的客戶端");
            return Results.Unauthorized();
        }

        if (!string.IsNullOrEmpty(clientSecret) && 
            !BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_client", 
                "客戶端認證失敗");
            return Results.Unauthorized();
        }

        // 執行 Token 輪替
        var rotationResult = await refreshTokenRotationService.RotateTokenAsync(
            refreshToken, clientId, new string[] { });
        
        if (!rotationResult.Success)
        {
            // 檢查是否為安全威脅
            if (rotationResult.SecurityCheck?.IsTokenReused == true)
            {
                await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                    $"Token重用偵測 - 威脅等級: {rotationResult.SecurityCheck.ThreatLevel}");
                return Results.BadRequest();
            }
            
            await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                rotationResult.ErrorMessage ?? "Token輪替失敗");
            return Results.BadRequest();
        }

        // 取得更新後的 Token 資訊來產生新的 Access Token
        var refreshTokenEntity = await refreshTokenRepository.GetByTokenAsync(rotationResult.NewToken!);
        if (refreshTokenEntity == null)
        {
            await errorService.WriteTokenErrorAsync(null!, "server_error", 
                "無法取得輪替後的Token資訊");
            return Results.StatusCode(500);
        }

        // 取得使用者資訊
        var user = await userRepository.GetByIdAsync(refreshTokenEntity.Subject);
        if (user == null)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_grant", 
                "使用者不存在");
            return Results.BadRequest();
        }

        // 產生新的 Access Token
        var scopes = refreshTokenEntity.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var accessToken = await GenerateAccessTokenAsync(user, client, scopes, 
            keyRepository, configuration);

        var response = new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60") * 60,
            refresh_token = rotationResult.NewToken,
            scope = string.Join(" ", scopes)
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleClientCredentialsGrant(
        IFormCollection form,
        IClientRepository clientRepository,
        IPersistedKeyRepository keyRepository,
        IErrorService errorService,
        IConfiguration configuration)
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var scope = form["scope"].ToString();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_request", 
                "缺少必要參數");
            return Results.BadRequest();
        }

        // 驗證客戶端
        var client = await clientRepository.GetByClientIdAsync(clientId);
        if (client == null || !client.IsActive)
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_client", 
                "無效的客戶端");
            return Results.Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_client", 
                "客戶端認證失敗");
            return Results.Unauthorized();
        }

        // 驗證範圍
        var requestedScopes = string.IsNullOrEmpty(scope) ? new[] { "api" } : scope.Split(' ');
        var allowedScopes = client.AllowedScopes.Split(' ');
        
        if (!requestedScopes.All(s => allowedScopes.Contains(s)))
        {
            await errorService.WriteTokenErrorAsync(null!, "invalid_scope", 
                "請求的範圍超出允許範圍");
            return Results.BadRequest();
        }

        // 產生Access Token (Client Credentials不包含Refresh Token)
        var accessToken = await GenerateAccessTokenAsync(null, client, requestedScopes, 
            keyRepository, configuration);

        var response = new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60") * 60,
            scope = string.Join(" ", requestedScopes)
        };

        return Results.Ok(response);
    }

    private static async Task<object> GenerateTokensAsync(
        Data.Entities.UserEntity user,
        Data.Entities.ClientEntity client,
        string[] scopes,
        IPersistedKeyRepository keyRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IConfiguration configuration,
        string? nonce = null)
    {
        var accessToken = await GenerateAccessTokenAsync(user, client, scopes, 
            keyRepository, configuration, nonce);
        
        var refreshToken = await GenerateRefreshTokenAsync(user, client, scopes, 
            refreshTokenRepository);

        var response = new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60") * 60,
            refresh_token = refreshToken,
            scope = string.Join(" ", scopes)
        };

        // 如果包含 openid scope，加入 id_token
        if (scopes.Contains("openid"))
        {
            var idToken = await GenerateIdTokenAsync(user, client, scopes, 
                keyRepository, configuration, nonce);
            
            return new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60") * 60,
                refresh_token = refreshToken,
                id_token = idToken,
                scope = string.Join(" ", scopes)
            };
        }

        return response;
    }

    private static async Task<string> GenerateAccessTokenAsync(
        Data.Entities.UserEntity? user,
        Data.Entities.ClientEntity client,
        string[] scopes,
        IPersistedKeyRepository keyRepository,
        IConfiguration configuration,
        string? nonce = null)
    {
        var key = await keyRepository.GetPrimarySigningKeyAsync();
        if (key == null)
        {
            throw new InvalidOperationException("找不到主要簽名金鑰");
        }

        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(key.EncryptedKeyData));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("iss", configuration["Jwt:Issuer"] ?? "https://localhost:5001"),
            new("aud", client.ClientId),
            new("client_id", client.ClientId),
            new("scope", string.Join(" ", scopes)),
            new("jti", Guid.NewGuid().ToString())
        };

        if (user != null)
        {
            claims.AddRange(new[]
            {
                new Claim("sub", user.Id),
                new Claim("username", user.UserName),
                new Claim("email", user.Email)
            });

            // 加入角色
            foreach (var role in user.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim("nonce", nonce));
        }

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60"));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: client.ClientId,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<string> GenerateIdTokenAsync(
        Data.Entities.UserEntity user,
        Data.Entities.ClientEntity client,
        string[] scopes,
        IPersistedKeyRepository keyRepository,
        IConfiguration configuration,
        string? nonce = null)
    {
        var key = await keyRepository.GetPrimarySigningKeyAsync();
        if (key == null)
        {
            throw new InvalidOperationException("找不到主要簽名金鑰");
        }

        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(key.EncryptedKeyData));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("iss", configuration["Jwt:Issuer"] ?? "https://localhost:5001"),
            new("aud", client.ClientId),
            new("sub", user.Id),
            new("auth_time", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString()),
            new("iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString())
        };

        if (scopes.Contains("profile"))
        {
            claims.AddRange(new[]
            {
                new Claim("name", user.UserName),
                new Claim("preferred_username", user.UserName)
            });
        }

        if (scopes.Contains("email"))
        {
            claims.AddRange(new[]
            {
                new Claim("email", user.Email),
                new Claim("email_verified", "true")
            });
        }

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim("nonce", nonce));
        }

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(int.Parse(configuration["Jwt:IdTokenExpirationMinutes"] ?? "60"));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: client.ClientId,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<IResult> HandleMfaGrant(
        IFormCollection form,
        HttpContext context,
        IUserRepository userRepository,
        IClientRepository clientRepository,
        IPersistedKeyRepository keyRepository,
        IErrorService errorService,
        IConfiguration configuration,
        LocalIdentityServer.Services.MFA.IMfaService mfaService)
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var mfaToken = form["mfa_token"].ToString(); // 原始的 authorization code
        var mfaCode = form["mfa_code"].ToString();
        var mfaMethod = form["mfa_method"].ToString();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(mfaToken) || 
            string.IsNullOrEmpty(mfaCode) || string.IsNullOrEmpty(mfaMethod))
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_request", 
                "Missing required parameters for MFA verification");
            return Results.BadRequest();
        }

        // 驗證客戶端
        var client = await clientRepository.GetByClientIdAsync(clientId);
        if (client == null || !client.IsActive)
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_client", 
                "Invalid client");
            return Results.Unauthorized();
        }

        if (!string.IsNullOrEmpty(clientSecret) && 
            !BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret))
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_client", 
                "Client authentication failed");
            return Results.Unauthorized();
        }

        // 驗證 MFA token (應該是原始的 authorization code)
        var authCodeRepository = context.RequestServices.GetRequiredService<IAuthorizationCodeRepository>();
        var authCode = await authCodeRepository.GetByCodeAsync(mfaToken);
        if (authCode == null || authCode.IsUsed || authCode.ExpiresAt <= DateTime.UtcNow)
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_grant", 
                "Invalid or expired MFA token");
            return Results.BadRequest();
        }

        // 取得使用者
        var user = await userRepository.GetByIdAsync(authCode.Subject);
        if (user == null)
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_grant", 
                "User not found");
            return Results.BadRequest();
        }

        // 檢查 MFA 是否啟用
        if (!await mfaService.IsMfaEnabledAsync(user.Id))
        {
            await errorService.WriteTokenErrorAsync(context, "invalid_request", 
                "MFA is not enabled for this user");
            return Results.BadRequest();
        }

        // 建立 MFA 驗證上下文
        var mfaContext = new LocalIdentityServer.Services.MFA.MfaVerificationContext
        {
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            ClientId = clientId,
            SessionId = context.TraceIdentifier,
            GeoLocation = null, // 可以添加地理位置服務
            DeviceFingerprint = context.Request.Headers["X-Device-Fingerprint"].ToString()
        };

        // 驗證 MFA 代碼
        var verificationResult = await mfaService.VerifyMfaCodeAsync(user.Id, mfaMethod, mfaCode, mfaContext);
        if (!verificationResult.IsValid)
        {
            var errorDetail = new
            {
                error = "invalid_grant",
                error_description = verificationResult.ErrorMessage ?? "MFA verification failed",
                remaining_attempts = verificationResult.RemainingAttempts,
                is_locked = verificationResult.IsLocked,
                locked_until = verificationResult.LockedUntil?.ToString("o")
            };

            await errorService.WriteTokenErrorAsync(context, "invalid_grant", 
                verificationResult.ErrorMessage ?? "MFA verification failed");
            return Results.BadRequest(errorDetail);
        }

        // MFA 驗證成功，標記 auth code 為 MFA 已驗證
        authCode.MfaVerified = "true";
        authCode.MfaMethod = mfaMethod;
        authCode.MfaVerifiedAt = DateTime.UtcNow;
        await authCodeRepository.StoreAsync(authCode);

        // 生成完整的 token 響應
        var refreshTokenRepository = context.RequestServices.GetRequiredService<IRefreshTokenRepository>();
        var tokens = await GenerateTokensAsync(user, client, authCode.Scope.Split(' '), 
            keyRepository, refreshTokenRepository, configuration, authCode.Nonce);

        // 標記 auth code 為已使用
        authCode.IsUsed = true;
        authCode.UsedAt = DateTime.UtcNow;
        await authCodeRepository.StoreAsync(authCode);

        return Results.Ok(tokens);
    }

    private static async Task<string> GenerateRefreshTokenAsync(
        Data.Entities.UserEntity user,
        Data.Entities.ClientEntity client,
        string[] scopes,
        IRefreshTokenRepository refreshTokenRepository)
    {
        var tokenValue = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        
        // 產生新的 Token 家族識別碼
        var tokenFamily = Guid.NewGuid().ToString("N");
        
        var refreshToken = new Data.Entities.RefreshTokenEntity
        {
            Token = tokenValue,
            TokenFamily = tokenFamily,
            ClientId = client.ClientId,
            Subject = user.Id,
            Username = user.UserName,
            Email = user.Email,
            Scope = string.Join(" ", scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(30), // 30天有效期
            IsUsed = false,
            IsRevoked = false
        };

        await refreshTokenRepository.CreateAsync(refreshToken);
        
        return tokenValue;
    }
}
