using CreditMonitoring.Common.Models;
using Microsoft.Extensions.Logging;

namespace CreditMonitoring.Common.Services
{
    /// <summary>
    /// 身份認證服務介面
    /// </summary>
    public interface IAuthenticationService
    {
        Task<JwtTokenResponse> AuthenticateAsync(JwtTokenRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<JwtTokenResponse?> RefreshTokenAsync(string token);
        Task LogoutAsync(string token);
        Task<JwtUserInfo?> GetUserInfoAsync(string token);
    }

    /// <summary>
    /// 身份認證服務實作
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly JwtTokenService _jwtTokenService;
        private readonly ILogger<AuthenticationService> _logger;

        // 模擬用戶資料庫
        private readonly List<UserAccount> _users = new()
        {
            new UserAccount
            {
                UserId = "U001",
                Username = "admin",
                Password = "admin123",
                UserType = "Admin",
                BankCode = "001",
                BranchCode = "0001",
                Roles = new[] { "Admin", "CreditOfficer", "Manager" }.ToList(),
                IsActive = true,
                Email = "admin@bank.com",
                FullName = "系統管理員"
            },
            new UserAccount
            {
                UserId = "U002",
                Username = "officer1",
                Password = "pass123",
                UserType = "CreditOfficer",
                BankCode = "001",
                BranchCode = "0001",
                Roles = new[] { "CreditOfficer" }.ToList(),
                IsActive = true,
                Email = "officer1@bank.com",
                FullName = "信貸專員一"
            },
            new UserAccount
            {
                UserId = "U003",
                Username = "manager1",
                Password = "mgr123",
                UserType = "Manager",
                BankCode = "001",
                BranchCode = "0001",
                Roles = new[] { "Manager", "CreditOfficer" }.ToList(),
                IsActive = true,
                Email = "manager1@bank.com",
                FullName = "分行經理"
            },
            new UserAccount
            {
                UserId = "U004",
                Username = "auditor1",
                Password = "aud123",
                UserType = "Auditor",
                BankCode = "001",
                BranchCode = null,
                Roles = new[] { "Auditor" }.ToList(),
                IsActive = true,
                Email = "auditor1@bank.com",
                FullName = "稽核人員"
            }
        };

        public AuthenticationService(JwtTokenService jwtTokenService, ILogger<AuthenticationService> logger)
        {
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task<JwtTokenResponse> AuthenticateAsync(JwtTokenRequest request)
        {
            try
            {
                _logger.LogInformation("開始身份認證: 用戶={Username}", request.Username);

                var user = _users.FirstOrDefault(u => u.Username == request.Username);
                if (user == null || user.Password != request.Password)
                {
                    return new JwtTokenResponse
                    {
                        Success = false,
                        ErrorMessage = "用戶名或密碼錯誤"
                    };
                }

                if (!user.IsActive)
                {
                    return new JwtTokenResponse
                    {
                        Success = false,
                        ErrorMessage = "用戶帳戶已被停用"
                    };
                }

                var token = _jwtTokenService.GenerateToken(
                    user.UserId,
                    user.Username,
                    user.UserType,
                    user.BankCode,
                    user.BranchCode,
                    user.Roles
                );

                return new JwtTokenResponse
                {
                    Success = true,
                    Token = token,
                    AccessToken = token,
                    TokenType = "Bearer",
                    ExpiresIn = 3600,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    UserInfo = new JwtUserInfo
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        UserType = user.UserType,
                        BankCode = user.BankCode,
                        BranchCode = user.BranchCode,
                        Roles = user.Roles
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "身份認證發生錯誤: {Username}", request.Username);
                return new JwtTokenResponse
                {
                    Success = false,
                    ErrorMessage = "系統錯誤，請稍後再試"
                };
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var (isValid, _) = _jwtTokenService.ValidateToken(token);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌驗證發生錯誤");
                return false;
            }
        }

        public async Task<JwtTokenResponse?> RefreshTokenAsync(string token)
        {
            try
            {
                var userInfo = _jwtTokenService.GetUserInfoFromToken(token);
                if (userInfo == null) return null;

                var user = _users.FirstOrDefault(u => u.UserId == userInfo.UserId);
                if (user == null || !user.IsActive) return null;

                var newToken = _jwtTokenService.GenerateToken(
                    user.UserId,
                    user.Username,
                    user.UserType,
                    user.BankCode,
                    user.BranchCode,
                    user.Roles
                );

                return new JwtTokenResponse
                {
                    Success = true,
                    Token = newToken,
                    AccessToken = newToken,
                    TokenType = "Bearer",
                    ExpiresIn = 3600,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    UserInfo = userInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌刷新發生錯誤");
                return null;
            }
        }

        public async Task LogoutAsync(string token)
        {
            try
            {
                var userInfo = _jwtTokenService.GetUserInfoFromToken(token);
                if (userInfo != null)
                {
                    _logger.LogInformation("用戶登出: {Username}", userInfo.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "用戶登出發生錯誤");
            }
        }

        public async Task<JwtUserInfo?> GetUserInfoAsync(string token)
        {
            try
            {
                return _jwtTokenService.GetUserInfoFromToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取用戶信息發生錯誤");
                return null;
            }
        }
    }

    /// <summary>
    /// 用戶帳戶模型
    /// </summary>
    public class UserAccount
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string UserType { get; set; } = "";
        public string BankCode { get; set; } = "";
        public string? BranchCode { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }
}
