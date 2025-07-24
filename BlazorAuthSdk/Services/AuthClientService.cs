using AuthCoreSdk.Models; // Use models from AuthCoreSdk
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BlazorAuthSdk.Services
{
    public class AuthClientService : IAuthClientService
    {
        private readonly HttpClient _httpClient;
        private readonly string _authenticationServerBaseUrl;

        public AuthClientService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authenticationServerBaseUrl = configuration["AuthenticationServer:BaseUrl"] ?? "https://localhost:7002"; // Default URL
            _httpClient.BaseAddress = new Uri(_authenticationServerBaseUrl);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code.

                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                return loginResponse ?? new LoginResponse { Token = null };
            }
            catch (HttpRequestException ex)
            {
                // Log the exception or handle specific HTTP error codes
                Console.WriteLine($"Error during login: {ex.Message}");
                return new LoginResponse { Token = null };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred during login: {ex.Message}");
                return new LoginResponse { Token = null };
            }
        }

        public UserInfo? GetUserInfoFromToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jsonToken == null)
                {
                    return null;
                }

                var userInfo = new UserInfo
                {
                    Id = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                    Username = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? 
                               jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                    Email = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value,
                    Role = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value,
                    Roles = jsonToken.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList()
                };

                return userInfo;
            }
            catch
            {
                return null;
            }
        }

        public bool IsTokenExpired(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jsonToken == null || jsonToken.ValidTo == DateTime.MinValue)
                {
                    return true; // Invalid token or no expiration date
                }

                // Check if token is expired (add a small buffer for clock skew if needed)
                return jsonToken.ValidTo < DateTime.UtcNow.AddMinutes(-1); // 1 minute buffer
            }
            catch
            {
                return true; // If parsing fails, consider it expired/invalid
            }
        }

        public JwtTokenDetails? GetTokenDetails(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jsonToken == null)
                {
                    return null;
                }

                return new JwtTokenDetails
                {
                    Issuer = jsonToken.Issuer,
                    Audiences = jsonToken.Audiences.ToList(),
                    ValidFrom = jsonToken.ValidFrom,
                    ValidTo = jsonToken.ValidTo,
                    Subject = jsonToken.Subject,
                    Claims = jsonToken.Claims.ToList()
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
