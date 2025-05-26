using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Common.Services
{
    /// <summary>
    /// JWT令牌服務 - 負責JWT的生成、驗證和解析
    /// 
    /// JWT (JSON Web Token) 是一種基於JSON的開放標準，用於在各方之間安全地傳輸信息。
    /// JWT由三部分組成：Header（標頭）、Payload（載荷）、Signature（簽名）
    /// 
    /// 優點：
    /// 1. 無狀態：伺服器不需要保存會話信息
    /// 2. 可擴展：適合分散式系統
    /// 3. 跨平台：支援多種語言和框架
    /// 4. 安全：使用數位簽名驗證令牌完整性
    /// 
    /// 使用場景：
    /// 1. 身份認證：用戶登入後獲取JWT令牌
    /// 2. 授權：API使用JWT驗證用戶權限
    /// 3. 信息交換：在系統間安全傳遞用戶信息
    /// </summary>
    public class JwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expirationMinutes;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;

            // 從配置中讀取JWT設定
            _secretKey = _configuration["Jwt:SecretKey"] ?? throw new ArgumentNullException("JWT SecretKey is required");
            _issuer = _configuration["Jwt:Issuer"] ?? "CreditMonitoring.Api";
            _audience = _configuration["Jwt:Audience"] ?? "CreditMonitoring.Web";
            _expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        }

        /// <summary>
        /// 生成JWT令牌
        /// 
        /// JWT結構說明：
        /// 1. Header（標頭）：包含令牌類型和加密算法
        ///    例如：{"typ":"JWT", "alg":"HS256"}
        /// 
        /// 2. Payload（載荷）：包含聲明（Claims），即用戶信息
        ///    標準聲明：iss(發行者)、sub(主題)、aud(受眾)、exp(過期時間)、iat(發行時間)
        ///    自定義聲明：userId、userType、bankCode等業務相關信息
        /// 
        /// 3. Signature（簽名）：用於驗證令牌是否被篡改
        ///    計算方式：HMAC_SHA256(base64UrlEncode(header) + "." + base64UrlEncode(payload), secret)
        /// </summary>
        /// <param name="userId">用戶ID</param>
        /// <param name="username">用戶名</param>
        /// <param name="userType">用戶類型（如：BankOfficer、Admin等）</param>
        /// <param name="bankCode">銀行代碼</param>
        /// <param name="branchCode">分行代碼</param>
        /// <param name="roles">用戶角色列表</param>
        /// <returns>JWT令牌字符串</returns>
        public string GenerateToken(
            string userId,
            string username,
            string userType,
            string? bankCode = null,
            string? branchCode = null,
            List<string>? roles = null)
        {
            // 創建對稱加密密鑰
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 創建聲明（Claims）- 這些是令牌中包含的用戶信息
            var claims = new List<Claim>
            {
                // 標準聲明
                new Claim(JwtRegisteredClaimNames.Sub, userId),           // Subject：主題，通常是用戶ID
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID：唯一標識符
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),                           // Issued At：發行時間
                
                // 自定義聲明 - 業務相關信息
                new Claim("userId", userId),
                new Claim("username", username),
                new Claim("userType", userType),
            };

            // 添加可選的銀行和分行信息
            if (!string.IsNullOrEmpty(bankCode))
                claims.Add(new Claim("bankCode", bankCode));

            if (!string.IsNullOrEmpty(branchCode))
                claims.Add(new Claim("branchCode", branchCode));

            // 添加角色信息
            if (roles != null && roles.Any())
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // 創建JWT令牌描述符
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),          // 主體：包含所有聲明
                Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes), // 過期時間
                Issuer = _issuer,                              // 發行者
                Audience = _audience,                          // 受眾
                SigningCredentials = credentials               // 簽名憑證
            };

            // 生成令牌
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// 驗證JWT令牌有效性
        /// 
        /// 驗證過程：
        /// 1. 檢查令牌格式是否正確
        /// 2. 驗證簽名是否有效（防止篡改）
        /// 3. 檢查發行者和受眾是否匹配
        /// 4. 驗證令牌是否已過期
        /// 5. 解析聲明信息
        /// </summary>
        /// <param name="token">要驗證的JWT令牌</param>
        /// <returns>驗證結果和聲明主體</returns>
        public (bool IsValid, ClaimsPrincipal? Principal) ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

                // 設定驗證參數
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,    // 驗證簽名密鑰
                    IssuerSigningKey = key,             // 簽名密鑰
                    ValidateIssuer = true,              // 驗證發行者
                    ValidIssuer = _issuer,              // 有效的發行者
                    ValidateAudience = true,            // 驗證受眾
                    ValidAudience = _audience,          // 有效的受眾
                    ValidateLifetime = true,            // 驗證生命週期
                    ClockSkew = TimeSpan.Zero           // 時鐘偏差容忍度
                };

                // 執行驗證
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                return (true, principal);
            }
            catch (SecurityTokenException)
            {
                // 令牌驗證失敗
                return (false, null);
            }
            catch (Exception)
            {
                // 其他異常
                return (false, null);
            }
        }

        /// <summary>
        /// 從JWT令牌中解析用戶信息
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <returns>用戶信息，如果解析失敗則返回null</returns>
        public JwtUserInfo? GetUserInfoFromToken(string token)
        {
            var (isValid, principal) = ValidateToken(token);

            if (!isValid || principal == null)
                return null;

            try
            {
                return new JwtUserInfo
                {
                    UserId = principal.FindFirst("userId")?.Value ?? "",
                    Username = principal.FindFirst("username")?.Value ?? "",
                    UserType = principal.FindFirst("userType")?.Value ?? "",
                    BankCode = principal.FindFirst("bankCode")?.Value,
                    BranchCode = principal.FindFirst("branchCode")?.Value,
                    Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 檢查JWT令牌是否即將過期（在指定分鐘數內）
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <param name="warningMinutes">警告時間（分鐘）</param>
        /// <returns>是否即將過期</returns>
        public bool IsTokenExpiringSoon(string token, int warningMinutes = 10)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);

                var expiration = jsonToken.ValidTo;
                var warningTime = DateTime.UtcNow.AddMinutes(warningMinutes);

                return expiration <= warningTime;
            }
            catch
            {
                return true; // 如果無法解析，則認為已過期
            }
        }
    }

}
