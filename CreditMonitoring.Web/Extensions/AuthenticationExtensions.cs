using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using CreditMonitoring.Web.Models;

namespace CreditMonitoring.Web.Extensions;

public static class AuthenticationExtensions
{
    public static AuthenticationBuilder AddMultipleOpenIdConnect(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var authConfig = configuration
            .GetSection("Authentication:Providers")
            .Get<Dictionary<string, AuthenticationProviderOptions>>();

        if (authConfig == null)
        {
            throw new InvalidOperationException("未找到認證提供者配置");
        }

        foreach (var provider in authConfig.Where(p => p.Value.Enabled))
        {
            builder.AddOpenIdConnect(provider.Key, options =>
            {
                options.Authority = provider.Value.Authority;
                options.ClientId = provider.Value.ClientId;
                options.ClientSecret = provider.Value.ClientSecret;
                options.ResponseType = provider.Value.ResponseType;
                options.SaveTokens = true;

                options.Scope.Clear();
                foreach (var scope in provider.Value.Scopes)
                {
                    options.Scope.Add(scope);
                }

                options.GetClaimsFromUserInfoEndpoint = true;

                // 統一聲明映射
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";

                // 事件處理
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var identity = context.Principal.Identity as ClaimsIdentity;
                        if (identity != null)
                        {
                            // 添加提供者聲明
                            identity.AddClaim(new Claim("provider", provider.Key));
                        }
                    }
                };
            });
        }

        return builder;
    }
}