using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SimpleJwtApi.Models;
using SimpleJwtApi.Services;

namespace SimpleJwtApi.Controllers;

/// <summary>
/// 認證控制器 - 學習 JWT 的基本 API 端點
/// 重點學習：
/// 1. 登入端點：接收用戶名密碼，返回 JWT
/// 2. 受保護端點：需要 JWT 才能訪問
/// 3. 用戶信息端點：從 JWT 中提取用戶信息
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwtService;

    // 模擬用戶資料庫 - 實際應用中會從資料庫讀取
    private static readonly List<User> Users = new()
    {
        new User
        {
            Id = "1",
            Username = "admin",
            Password = "123456", // 實際應用中應該加密
            Email = "admin@example.com",
            Roles = new List<string> { "Admin", "User" }
        },
        new User
        {
            Id = "2",
            Username = "user",
            Password = "123456",
            Email = "user@example.com",
            Roles = new List<string> { "User" }
        }
    };

    public AuthController(JwtService jwtService)
    {
        _jwtService = jwtService;
    }

    /// <summary>
    /// 用戶登入
    /// 最重要的端點：驗證用戶並生成 JWT 令牌
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        // 1. 驗證用戶輸入
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "用戶名和密碼不能為空"
            });
        }

        // 2. 查找用戶 - 實際應用中會查詢資料庫
        var user = Users.FirstOrDefault(u =>
            u.Username == request.Username && u.Password == request.Password);

        if (user == null)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                Message = "用戶名或密碼錯誤"
            });
        }

        // 3. 生成 JWT 令牌
        var token = _jwtService.GenerateToken(user);

        // 4. 返回成功響應
        return Ok(new LoginResponse
        {
            Success = true,
            Token = token,
            Message = "登入成功",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles
            }
        });
    }

    /// <summary>
    /// 獲取當前用戶信息 (需要 JWT)
    /// 展示如何保護 API 端點和從令牌中提取信息
    /// </summary>
    [HttpGet("me")]
    [Authorize] // 這個屬性要求請求必須包含有效的 JWT
    public ActionResult<ApiResponse<UserInfo>> GetCurrentUser()
    {
        // 從 HTTP 頭中獲取 JWT 令牌
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new ApiResponse<UserInfo>
            {
                Success = false,
                Message = "缺少有效的 Authorization 標頭"
            });
        }

        // 提取令牌（移除 "Bearer " 前綴）
        var token = authHeader.Substring("Bearer ".Length).Trim();

        // 從令牌中提取用戶信息
        var userInfo = _jwtService.GetUserInfoFromToken(token);
        if (userInfo == null)
        {
            return Unauthorized(new ApiResponse<UserInfo>
            {
                Success = false,
                Message = "無效的令牌"
            });
        }

        return Ok(new ApiResponse<UserInfo>
        {
            Success = true,
            Message = "獲取用戶信息成功",
            Data = userInfo
        });
    }

    /// <summary>
    /// 需要管理員權限的端點
    /// 展示基於角色的授權
    /// </summary>
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public ActionResult<ApiResponse<string>> AdminOnly()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "您有管理員權限！",
            Data = "這是只有管理員能看到的內容"
        });
    }

    /// <summary>
    /// 公開端點（不需要 JWT）
    /// 用於測試 API 是否正常運行
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<string>> Public()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "這是公開端點",
            Data = $"當前時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}"
        });
    }
}