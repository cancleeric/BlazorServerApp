using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SimpleJwtApi.Models;
using System.Security.Claims;

namespace SimpleJwtApi.Controllers;

/// <summary>
/// 認證控制器 - 作為資源伺服器，驗證 JWT 並提供受保護的端點。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// 獲取當前用戶信息 (需要 JWT)
    /// 展示如何保護 API 端點和從令牌中提取信息
    /// </summary>
    [HttpGet("me")]
    [Authorize] // 這個屬性要求請求必須包含有效的 JWT
    public ActionResult<ApiResponse<UserInfo>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; // Assuming Sub claim is NameIdentifier
        var username = User.FindFirst(ClaimTypes.Name)?.Value; // Assuming Name claim is Username
        var role = User.FindFirst(ClaimTypes.Role)?.Value; // Assuming Role claim is Role

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized(new ApiResponse<UserInfo>
            {
                Success = false,
                Message = "無法從令牌中獲取用戶信息"
            });
        }

        var userInfo = new UserInfo
        {
            Id = userId,
            Username = username,
            Roles = new List<string> { role } // Assuming single role for simplicity
        };

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
