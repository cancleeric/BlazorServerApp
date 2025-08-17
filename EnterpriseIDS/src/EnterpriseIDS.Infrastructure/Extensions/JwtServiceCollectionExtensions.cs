using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Data.Repositories;
using EnterpriseIDS.Infrastructure.Services;

namespace EnterpriseIDS.Infrastructure.Extensions;

/// <summary>
/// JWT 服務註冊擴展
/// </summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 JWT Token 服務
    /// </summary>
    public static IServiceCollection AddJwtTokenServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置 JWT 設定
        services.Configure<JwtTokenSettings>(configuration.GetSection("JwtTokenSettings"));

        // 註冊 Repository
        services.AddScoped<IJwtTokenRepository, JwtTokenRepository>();
        services.AddScoped<ITokenBlacklistRepository, TokenBlacklistRepository>();

        // 註冊服務
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    /// <summary>
    /// 配置 JWT 驗證
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtTokenSettings").Get<JwtTokenSettings>()
            ?? throw new InvalidOperationException("JwtTokenSettings not configured");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                IssuerSigningKey = GetSigningKey(jwtSettings),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(jwtSettings.ClockSkewMinutes),
                NameClaimType = "username",
                RoleClaimType = "role"
            };

            // 自定義 Token 驗證事件
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var jwtTokenService = context.HttpContext.RequestServices
                        .GetRequiredService<IJwtTokenService>();

                    var jwtId = context.Principal?.FindFirst("jti")?.Value;
                    if (!string.IsNullOrEmpty(jwtId))
                    {
                        // 檢查 Token 是否在黑名單中
                        var isBlacklisted = await jwtTokenService.IsTokenBlacklistedAsync(jwtId);
                        if (isBlacklisted)
                        {
                            context.Fail("Token is blacklisted");
                            return;
                        }

                        // 更新 Token 使用統計
                        var tokenRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IJwtTokenRepository>();

                        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();
                        var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();

                        await tokenRepository.UpdateTokenUsageAsync(jwtId, ipAddress, userAgent);
                    }
                },

                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Add("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                },

                OnChallenge = context =>
                {
                    context.HandleResponse();
                    
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";

                    var result = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "unauthorized",
                        error_description = "The access token is missing or invalid"
                    });

                    return context.Response.WriteAsync(result);
                },

                OnForbidden = context =>
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";

                    var result = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "forbidden",
                        error_description = "Insufficient permissions to access this resource"
                    });

                    return context.Response.WriteAsync(result);
                }
            };
        });

        return services;
    }

    /// <summary>
    /// 取得簽章金鑰
    /// </summary>
    private static SecurityKey GetSigningKey(JwtTokenSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.PublicKey))
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(settings.PublicKey), out _);
            return new RsaSecurityKey(rsa);
        }

        // 如果沒有 RSA 公鑰，使用 HMAC
        var key = Encoding.UTF8.GetBytes(settings.SecretKey);
        return new SymmetricSecurityKey(key);
    }
}