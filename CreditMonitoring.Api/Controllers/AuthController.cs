using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Common.Services;

namespace CreditMonitoring.Api.Controllers
{
    /// <summary>
    /// 身份認證控制器
    /// 
    /// 提供JWT身份認證相關的API端點：
    /// 1. 用戶登入（獲取JWT令牌）
    /// 2. 令牌驗證
    /// 3. 令牌刷新
    /// 4. 用戶登出
    /// 5. 獲取當前用戶信息
    /// 
    /// RESTful API設計原則：
    /// - POST /auth/login - 用戶登入
    /// - POST /auth/refresh - 刷新令牌
    /// - POST /auth/logout - 用戶登出
    /// - GET /auth/me - 獲取當前用戶信息
    /// - POST /auth/validate - 驗證令牌
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// 用戶登入
        /// 
        /// HTTP POST /api/auth/login
        /// 
        /// 請求範例：
        /// {
        ///   "username": "admin",
        ///   "password": "admin123",
        ///   "bankCode": "001",
        ///   "branchCode": "0001"
        /// }
        /// 
        /// 成功響應：
        /// {
        ///   "success": true,
        ///   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        ///   "expiresAt": "2024-05-26T10:00:00Z",
        ///   "userInfo": {
        ///     "userId": "U001",
        ///     "username": "admin",
        ///     "userType": "Admin",
        ///     "bankCode": "001",
        ///     "branchCode": "0001",
        ///     "roles": ["Admin", "CreditOfficer"]
        ///   }
        /// }
        /// </summary>
        /// <param name="request">登入請求</param>
        /// <returns>JWT令牌響應</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<JwtTokenResponse>> Login([FromBody] JwtTokenRequest request)
        {
            try
            {
                // 記錄登入嘗試
                _logger.LogInformation("收到登入請求: 用戶={Username}, IP={ClientIP}", 
                    request.Username, HttpContext.Connection.RemoteIpAddress);

                // 驗證請求參數
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("登入請求參數不完整: {Username}", request.Username);
                    return BadRequest(new JwtTokenResponse
                    {
                        Success = false,
                        ErrorMessage = "用戶名和密碼不能為空"
                    });
                }

                // 執行身份認證
                var response = await _authService.AuthenticateAsync(request);

                // 根據認證結果返回適當的HTTP狀態碼
                if (response.Success)
                {
                    _logger.LogInformation("用戶登入成功: {Username}", request.Username);
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning("用戶登入失敗: {Username}, 原因: {Error}", 
                        request.Username, response.ErrorMessage);
                    return Unauthorized(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登入處理發生異常: {Username}", request.Username);
                return StatusCode(500, new JwtTokenResponse
                {
                    Success = false,
                    ErrorMessage = "伺服器內部錯誤"
                });
            }
        }

        /// <summary>
        /// 刷新JWT令牌
        /// 
        /// HTTP POST /api/auth/refresh
        /// Header: Authorization: Bearer {current_token}
        /// 
        /// 使用場景：
        /// 1. 令牌即將過期時自動刷新
        /// 2. 延長用戶會話時間
        /// 3. 更新用戶權限信息
        /// </summary>
        /// <returns>新的JWT令牌</returns>
        [HttpPost("refresh")]
        [Authorize]
        public async Task<ActionResult<JwtTokenResponse>> RefreshToken()
        {
            try
            {
                // 從Authorization標頭中提取令牌
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("刷新令牌請求缺少Authorization標頭");
                    return BadRequest(new JwtTokenResponse
                    {
                        Success = false,
                        ErrorMessage = "缺少Authorization標頭"
                    });
                }

                var currentToken = authHeader.Substring("Bearer ".Length).Trim();
                var response = await _authService.RefreshTokenAsync(currentToken);

                if (response != null)
                {
                    _logger.LogInformation("令牌刷新成功");
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning("令牌刷新失敗");
                    return Unauthorized(new JwtTokenResponse
                    {
                        Success = false,
                        ErrorMessage = "令牌刷新失敗"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌刷新發生異常");
                return StatusCode(500, new JwtTokenResponse
                {
                    Success = false,
                    ErrorMessage = "伺服器內部錯誤"
                });
            }
        }

        /// <summary>
        /// 用戶登出
        /// 
        /// HTTP POST /api/auth/logout
        /// Header: Authorization: Bearer {token}
        /// 
        /// 登出操作：
        /// 1. 將令牌加入黑名單（可選）
        /// 2. 清除伺服器端會話信息
        /// 3. 記錄登出日誌
        /// </summary>
        /// <returns>登出結果</returns>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader != null && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    await _authService.LogoutAsync(token);
                }

                _logger.LogInformation("用戶登出成功");
                return Ok(new { Success = true, Message = "登出成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "用戶登出發生異常");
                return StatusCode(500, new { Success = false, Message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 獲取當前用戶信息
        /// 
        /// HTTP GET /api/auth/me
        /// Header: Authorization: Bearer {token}
        /// 
        /// 響應範例：
        /// {
        ///   "userId": "U001",
        ///   "username": "admin",
        ///   "userType": "Admin",
        ///   "bankCode": "001",
        ///   "branchCode": "0001",
        ///   "roles": ["Admin", "CreditOfficer"]
        /// }
        /// </summary>
        /// <returns>當前用戶信息</returns>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<JwtUserInfo>> GetCurrentUser()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer "))
                {
                    return BadRequest(new { Message = "缺少Authorization標頭" });
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var userInfo = await _authService.GetUserInfoAsync(token);

                if (userInfo != null)
                {
                    return Ok(userInfo);
                }
                else
                {
                    return Unauthorized(new { Message = "無效的令牌" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取用戶信息發生異常");
                return StatusCode(500, new { Message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 驗證JWT令牌
        /// 
        /// HTTP POST /api/auth/validate
        /// 
        /// 請求範例：
        /// {
        ///   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
        /// }
        /// 
        /// 響應範例：
        /// {
        ///   "isValid": true,
        ///   "message": "令牌有效"
        /// }
        /// </summary>
        /// <param name="request">驗證請求</param>
        /// <returns>驗證結果</returns>
        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<ActionResult> ValidateToken([FromBody] TokenValidationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { IsValid = false, Message = "令牌不能為空" });
                }

                var isValid = await _authService.ValidateTokenAsync(request.Token);
                
                return Ok(new 
                { 
                    IsValid = isValid, 
                    Message = isValid ? "令牌有效" : "令牌無效或已過期" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "令牌驗證發生異常");
                return StatusCode(500, new { IsValid = false, Message = "伺服器內部錯誤" });
            }
        }
    }

    /// <summary>
    /// 令牌驗證請求模型
    /// </summary>
    public class TokenValidationRequest
    {
        public string Token { get; set; } = "";
    }
}
