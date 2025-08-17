using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Presentation.Controllers;

/// <summary>
/// Token 管理 API 控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TokenController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TokenController> _logger;

    public TokenController(
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository,
        ILogger<TokenController> logger)
    {
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// OAuth 2.0 Token 端點
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        try
        {
            return request.GrantType switch
            {
                "authorization_code" => await HandleAuthorizationCodeGrant(request),
                "refresh_token" => await HandleRefreshTokenGrant(request),
                "client_credentials" => await HandleClientCredentialsGrant(request),
                _ => BadRequest(new TokenErrorResponse
                {
                    Error = "unsupported_grant_type",
                    ErrorDescription = $"Grant type '{request.GrantType}' is not supported"
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token 端點處理失敗: {GrantType}", request.GrantType);
            return StatusCode(500, new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An internal server error occurred"
            });
        }
    }

    /// <summary>
    /// 撤銷 Token
    /// </summary>
    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromForm] RevokeTokenRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new TokenErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Token parameter is required"
                });
            }

            var currentUserId = GetCurrentUserId();
            var success = await _jwtTokenService.RevokeTokenAsync(
                request.Token,
                "User revoked",
                currentUserId,
                revokeAssociatedTokens: true);

            if (!success)
            {
                return BadRequest(new TokenErrorResponse
                {
                    Error = "invalid_token",
                    ErrorDescription = "Token not found or already revoked"
                });
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤銷 Token 失敗");
            return StatusCode(500, new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "Failed to revoke token"
            });
        }
    }

    /// <summary>
    /// 撤銷使用者的所有 Token
    /// </summary>
    [HttpPost("revoke-all")]
    [Authorize]
    public async Task<IActionResult> RevokeAllTokens()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentTokenId = GetCurrentTokenId();

            // 排除當前 Token，避免撤銷後無法回應
            var excludeTokenIds = currentTokenId.HasValue ? new[] { currentTokenId.Value } : null;

            var revokedCount = await _jwtTokenService.RevokeUserTokensAsync(
                currentUserId,
                "User revoked all tokens",
                currentUserId,
                excludeTokenIds);

            return Ok(new
            {
                message = "All tokens revoked successfully",
                revoked_count = revokedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "撤銷所有 Token 失敗");
            return StatusCode(500, new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "Failed to revoke all tokens"
            });
        }
    }

    /// <summary>
    /// 取得當前使用者的 Token 清單
    /// </summary>
    [HttpGet("list")]
    [Authorize]
    public async Task<IActionResult> ListTokens([FromQuery] string? tokenType = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var tokens = await _jwtTokenService.GetUserTokensAsync(
                currentUserId,
                tokenType,
                includeExpired: false,
                includeRevoked: false);

            var tokenList = tokens.Select(t => new TokenInfo
            {
                Id = t.Id,
                JwtId = t.JwtId,
                TokenType = t.TokenType,
                IssuedAt = t.IssuedAt,
                ExpiresAt = t.ExpiresAt,
                LastUsedAt = t.LastUsedAt,
                UseCount = t.UseCount,
                SourceIpAddress = t.SourceIpAddress,
                UserAgent = t.UserAgent,
                IsActive = t.IsValid(),
                IsNearExpiry = t.IsNearExpiry(),
                Scopes = t.GetScopesArray()
            });

            return Ok(new
            {
                tokens = tokenList,
                total_count = tokenList.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 Token 清單失敗");
            return StatusCode(500, new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "Failed to retrieve token list"
            });
        }
    }

    /// <summary>
    /// 取得 Token 統計資訊
    /// </summary>
    [HttpGet("statistics")]
    [Authorize]
    public async Task<IActionResult> GetTokenStatistics()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var statistics = await _jwtTokenService.GetTokenStatisticsAsync(currentUserId);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 Token 統計失敗");
            return StatusCode(500, new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "Failed to retrieve token statistics"
            });
        }
    }

    /// <summary>
    /// 驗證 Token
    /// </summary>
    [HttpPost("introspect")]
    [Authorize]
    public async Task<IActionResult> IntrospectToken([FromForm] IntrospectRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new TokenErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Token parameter is required"
                });
            }

            var validationResult = await _jwtTokenService.ValidateTokenAsync(request.Token);

            if (!validationResult.IsValid)
            {
                return Ok(new IntrospectResponse
                {
                    Active = false
                });
            }

            return Ok(new IntrospectResponse
            {
                Active = true,
                TokenType = validationResult.TokenType,
                ExpiresAt = validationResult.ExpiresAt,
                UserId = validationResult.UserId,
                TenantId = validationResult.TenantId,
                Scopes = validationResult.Claims
                    .Where(c => c.Type == "scope")
                    .SelectMany(c => c.Value.Split(' '))
                    .ToArray()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token 內省失敗");
            return StatusCode(500, new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "Failed to introspect token"
            });
        }
    }

    #region 私有方法

    /// <summary>
    /// 處理授權碼授予
    /// </summary>
    private async Task<IActionResult> HandleAuthorizationCodeGrant(TokenRequest request)
    {
        // 這裡應該驗證授權碼，但簡化實作
        // 實際應用中需要實作完整的 OAuth 2.0 授權碼流程
        
        // 暫時使用基本的使用者名稱/密碼驗證作為演示
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new TokenErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "Username and password are required for demonstration"
            });
        }

        // 簡化的使用者驗證
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null || !user.IsActive())
        {
            return BadRequest(new TokenErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid username or password"
            });
        }

        // 生成 Token
        var scopes = !string.IsNullOrEmpty(request.Scope) ? request.Scope.Split(' ') : new[] { "openid", "profile" };
        
        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(
            user.Id,
            user.TenantId,
            scopes: scopes,
            clientId: request.ClientId,
            audience: request.ClientId);

        var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(
            user.Id,
            user.TenantId,
            request.ClientId,
            scopes);

        var idToken = await _jwtTokenService.GenerateIdTokenAsync(
            user.Id,
            user.TenantId,
            request.ClientId ?? "default");

        return Ok(new TokenResponse
        {
            AccessToken = accessToken.Token,
            TokenType = "Bearer",
            ExpiresIn = accessToken.ExpiresIn,
            RefreshToken = refreshToken.Token,
            IdToken = idToken.Token,
            Scope = string.Join(" ", scopes)
        });
    }

    /// <summary>
    /// 處理刷新 Token 授予
    /// </summary>
    private async Task<IActionResult> HandleRefreshTokenGrant(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new TokenErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "Refresh token is required"
            });
        }

        try
        {
            var newScopes = !string.IsNullOrEmpty(request.Scope) ? request.Scope.Split(' ') : null;
            var result = await _jwtTokenService.RefreshAccessTokenAsync(request.RefreshToken, newScopes);

            var response = new TokenResponse
            {
                AccessToken = result.AccessToken.Token,
                TokenType = "Bearer",
                ExpiresIn = result.AccessToken.ExpiresIn,
                Scope = string.Join(" ", result.AccessToken.Scopes)
            };

            if (result.RefreshTokenRotated && result.RefreshToken != null)
            {
                response.RefreshToken = result.RefreshToken.Token;
            }

            return Ok(response);
        }
        catch (SecurityTokenException ex)
        {
            return BadRequest(new TokenErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = ex.Message
            });
        }
    }

    /// <summary>
    /// 處理客戶端憑證授予
    /// </summary>
    private async Task<IActionResult> HandleClientCredentialsGrant(TokenRequest request)
    {
        // 簡化的客戶端驗證
        if (string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
        {
            return BadRequest(new TokenErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Client authentication failed"
            });
        }

        // 這裡應該驗證客戶端憑證
        // 暫時創建一個系統使用者 Token
        var systemUserId = Guid.Empty; // 系統使用者
        var systemTenantId = Guid.Empty; // 系統租戶

        var scopes = !string.IsNullOrEmpty(request.Scope) ? request.Scope.Split(' ') : new[] { "api" };

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(
            systemUserId,
            systemTenantId,
            scopes: scopes,
            clientId: request.ClientId);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken.Token,
            TokenType = "Bearer",
            ExpiresIn = accessToken.ExpiresIn,
            Scope = string.Join(" ", scopes)
        });
    }

    /// <summary>
    /// 取得當前使用者 ID
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// 取得當前 Token ID
    /// </summary>
    private Guid? GetCurrentTokenId()
    {
        var jtiClaim = User.FindFirst("jti");
        if (jtiClaim != null && Guid.TryParse(jtiClaim.Value, out var tokenId))
        {
            return tokenId;
        }
        return null;
    }

    #endregion
}

#region DTO 類別

/// <summary>
/// Token 請求
/// </summary>
public class TokenRequest
{
    public string GrantType { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? RedirectUri { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// Token 回應
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// Token 錯誤回應
/// </summary>
public class TokenErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? ErrorDescription { get; set; }
    public string? ErrorUri { get; set; }
}

/// <summary>
/// 撤銷 Token 請求
/// </summary>
public class RevokeTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string? TokenTypeHint { get; set; }
}

/// <summary>
/// Token 內省請求
/// </summary>
public class IntrospectRequest
{
    public string Token { get; set; } = string.Empty;
    public string? TokenTypeHint { get; set; }
}

/// <summary>
/// Token 內省回應
/// </summary>
public class IntrospectResponse
{
    public bool Active { get; set; }
    public string? TokenType { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string[] Scopes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Token 資訊
/// </summary>
public class TokenInfo
{
    public Guid Id { get; set; }
    public string JwtId { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; }
    public bool IsNearExpiry { get; set; }
    public string[] Scopes { get; set; } = Array.Empty<string>();
}

#endregion