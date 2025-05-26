using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SimpleJwtApi.Models;

namespace SimpleJwtApi.Services;

/// <summary>
/// 最簡單的 JWT 服務
/// 學習重點：
/// 1. 如何生成 JWT 令牌
/// 2. 如何驗證 JWT 令牌
/// 3. 如何從令牌中提取用戶信息
/// </summary>
public class JwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtService(IConfiguration configuration)
    {
        // 從配置檔案讀取 JWT 設定
        _secretKey = configuration["Jwt:SecretKey"] ?? "MyVerySecretKeyForJwtTokenThatIsAtLeast256Bits";
        _issuer = configuration["Jwt:Issuer"] ?? "SimpleJwtApi";
        _audience = configuration["Jwt:Audience"] ?? "SimpleJwtApiUsers";
        _expirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "60");
    }

    /// <summary>
    /// 生成 JWT 令牌
    /// 這是 JWT 的核心功能：將用戶信息編碼成安全的令牌
    /// </summary>
    public string GenerateToken(User user)
    {
        // 1. 準備簽名密鑰
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 2. 創建用戶聲明 (Claims) - 這些信息會被包含在令牌中
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // 3. 添加角色信息
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 4. 創建令牌
        var token = new JwtSecurityToken(
            issuer: _issuer,                                          // 發行者
            audience: _audience,                                      // 受眾
            claims: claims,                                           // 聲明
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes), // 過期時間
            signingCredentials: credentials                           // 簽名憑證
        );

        // 5. 將令牌物件轉換為字符串
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 驗證 JWT 令牌
    /// 確認令牌是否有效、未過期、未被篡改
    /// </summary>
    public bool ValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

            // 設定驗證參數
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,     // 驗證簽名密鑰
                IssuerSigningKey = key,              // 設定密鑰
                ValidateIssuer = true,               // 驗證發行者
                ValidIssuer = _issuer,
                ValidateAudience = true,             // 驗證受眾
                ValidAudience = _audience,
                ValidateLifetime = true,             // 驗證過期時間
                ClockSkew = TimeSpan.Zero            // 不允許時間偏差
            };

            // 執行驗證
            principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 從令牌中提取用戶信息
    /// 這個方法展示如何從 JWT 中讀取用戶數據
    /// </summary>
    public UserInfo? GetUserInfoFromToken(string token)
    {
        if (!ValidateToken(token, out var principal) || principal == null)
            return null;

        return new UserInfo
        {
            Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",
            Username = principal.FindFirst(ClaimTypes.Name)?.Value ?? "",
            Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? "",
            Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
        };
    }
}