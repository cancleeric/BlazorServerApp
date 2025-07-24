using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using AuthCoreSdk.Models;
using BlazorAuthSdk.Services;

namespace BlazorAuthSdk.Authentication
{
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IAuthClientService _authClientService;

        public JwtAuthenticationStateProvider(IJSRuntime jsRuntime, IAuthClientService authClientService)
        {
            _jsRuntime = jsRuntime;
            _authClientService = authClientService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");

            if (string.IsNullOrEmpty(token) || _authClientService.IsTokenExpired(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var userInfo = _authClientService.GetUserInfoFromToken(token);
            if (userInfo == null)
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userInfo.Username ?? ""),
                new Claim(ClaimTypes.NameIdentifier, userInfo.Id ?? ""),
            };

            if (userInfo.Roles != null)
            {
                foreach (var role in userInfo.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
            else if (!string.IsNullOrEmpty(userInfo.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, userInfo.Role));
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }

        public void MarkUserAsAuthenticated(string token)
        {
            var userInfo = _authClientService.GetUserInfoFromToken(token);
            if (userInfo == null)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
                return;
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userInfo.Username ?? ""),
                new Claim(ClaimTypes.NameIdentifier, userInfo.Id ?? ""),
            };

            if (userInfo.Roles != null)
            {
                foreach (var role in userInfo.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
            else if (!string.IsNullOrEmpty(userInfo.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, userInfo.Role));
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void MarkUserAsLoggedOut()
        {
            var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
        }
    }
}
