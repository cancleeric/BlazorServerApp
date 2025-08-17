using OtpNet;
using System.Security.Cryptography;
using System.Text;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// TOTP (Time-based One-Time Password) 服務實作
/// 遵循 RFC 6238 標準，支援 Google Authenticator 和 Microsoft Authenticator
/// </summary>
public class TotpService : ITotpService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TotpService> _logger;
    private readonly int _timeStepSeconds;
    private readonly int _digits;
    private readonly OtpHashMode _hashMode;

    public TotpService(IConfiguration configuration, ILogger<TotpService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // 從設定檔讀取 TOTP 參數，預設值符合 RFC 6238
        _timeStepSeconds = int.Parse(_configuration["Mfa:Totp:TimeStepSeconds"] ?? "30");
        _digits = int.Parse(_configuration["Mfa:Totp:Digits"] ?? "6");
        
        var hashModeString = _configuration["Mfa:Totp:HashMode"] ?? "Sha1";
        _hashMode = Enum.Parse<OtpHashMode>(hashModeString, true);
    }

    public string GenerateSecret()
    {
        try
        {
            // 產生 160 位 (20 字節) 的隨機密鑰，符合 RFC 4226 建議
            var keyBytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            // 轉換為 Base32 編碼
            var secret = Base32Encoding.ToString(keyBytes);
            
            _logger.LogDebug("Generated new TOTP secret with length: {Length}", secret.Length);
            
            return secret;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate TOTP secret");
            throw new InvalidOperationException("Failed to generate TOTP secret", ex);
        }
    }

    public string GenerateTotpUri(string userIdentifier, string secret, string issuer = "LocalIdentityServer", string? accountName = null)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
            throw new ArgumentException("使用者識別碼不可為空", nameof(userIdentifier));
        
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("TOTP 密鑰不可為空", nameof(secret));
        
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("發行者不可為空", nameof(issuer));

        try
        {
            // 驗證密鑰格式
            if (!IsValidSecret(secret))
                throw new ArgumentException("TOTP 密鑰格式無效", nameof(secret));

            // 建構符合 Google Authenticator 標準的 URI
            var encodedUser = Uri.EscapeDataString(userIdentifier);
            var encodedIssuer = Uri.EscapeDataString(issuer);
            var label = string.IsNullOrWhiteSpace(accountName) 
                ? $"{encodedIssuer}:{encodedUser}"
                : $"{encodedIssuer}:{Uri.EscapeDataString(accountName)}";
            
            var uri = $"otpauth://totp/{label}" +
                     $"?secret={secret}" +
                     $"&issuer={encodedIssuer}" +
                     $"&algorithm={_hashMode.ToString().ToUpper()}" +
                     $"&digits={_digits}" +
                     $"&period={_timeStepSeconds}";

            _logger.LogDebug("Generated TOTP URI for user: {UserIdentifier}, issuer: {Issuer}", userIdentifier, issuer);
            
            return uri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate TOTP URI for user: {UserIdentifier}", userIdentifier);
            throw;
        }
    }

    public TotpVerificationResult VerifyTotp(string secret, string code, int timeWindow = 30)
    {
        return VerifyTotpWithTolerance(secret, code, 0);
    }

    public TotpVerificationResult VerifyTotpWithTolerance(string secret, string code, int toleranceSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return TotpVerificationResult.Failure("TOTP 密鑰不可為空");

        if (string.IsNullOrWhiteSpace(code))
            return TotpVerificationResult.Failure("TOTP 代碼不可為空");

        try
        {
            // 驗證密鑰格式
            if (!IsValidSecret(secret))
                return TotpVerificationResult.Failure("TOTP 密鑰格式無效");

            // 清理代碼 (移除空格和特殊字符)
            var cleanCode = new string(code.Where(char.IsDigit).ToArray());
            
            if (cleanCode.Length != _digits)
                return TotpVerificationResult.Failure($"TOTP 代碼必須為 {_digits} 位數字");

            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes, _timeStepSeconds, _hashMode, _digits);
            
            var currentTime = DateTime.UtcNow;
            var remainingTime = GetRemainingTime();

            // 檢查當前時間窗口
            if (totp.VerifyTotp(cleanCode, out var timeStepMatched, VerificationWindow.RfcSpecifiedNetworkDelay))
            {
                return TotpVerificationResult.Success(0, remainingTime);
            }

            // 如果支援容錯，檢查前後時間窗口
            if (toleranceSteps > 0)
            {
                // 檢查過去的時間窗口
                for (int step = 1; step <= toleranceSteps; step++)
                {
                    var pastTime = currentTime.AddSeconds(-step * _timeStepSeconds);
                    var pastTotp = totp.ComputeTotp(pastTime);
                    
                    if (pastTotp == cleanCode)
                    {
                        _logger.LogWarning("TOTP code from previous time window used - potential replay attack");
                        return TotpVerificationResult.Success(-step, remainingTime);
                    }
                }

                // 檢查未來的時間窗口 (考慮時鐘偏移)
                for (int step = 1; step <= toleranceSteps; step++)
                {
                    var futureTime = currentTime.AddSeconds(step * _timeStepSeconds);
                    var futureTotp = totp.ComputeTotp(futureTime);
                    
                    if (futureTotp == cleanCode)
                    {
                        return TotpVerificationResult.Success(step, remainingTime);
                    }
                }
            }

            _logger.LogWarning("TOTP verification failed: invalid code provided");
            return TotpVerificationResult.Failure("TOTP 代碼無效或已過期");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TOTP verification");
            return TotpVerificationResult.Failure($"TOTP 驗證過程發生錯誤: {ex.Message}");
        }
    }

    public string GetCurrentTotp(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));

        if (!IsValidSecret(secret))
            throw new ArgumentException("Invalid TOTP secret format", nameof(secret));

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes, _timeStepSeconds, _hashMode, _digits);
            
            var currentCode = totp.ComputeTotp();
            
            _logger.LogDebug("Generated current TOTP code");
            
            return currentCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate current TOTP code");
            throw new InvalidOperationException("Failed to generate current TOTP code", ex);
        }
    }

    public int GetRemainingTime(int timeWindow = 30)
    {
        var unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
        var remainingSeconds = timeWindow - (int)(unixTime % timeWindow);
        return remainingSeconds == timeWindow ? 0 : remainingSeconds;
    }

    public bool IsValidSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        try
        {
            // 檢查 Base32 格式
            if (!System.Text.RegularExpressions.Regex.IsMatch(secret, @"^[A-Z2-7]+=*$"))
                return false;

            // 嘗試解碼
            var bytes = Base32Encoding.ToBytes(secret);
            
            // 檢查密鑰長度 (至少 128 位，建議 160 位)
            return bytes.Length >= 16;
        }
        catch
        {
            return false;
        }
    }

    private long GetCurrentTimeStep()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() / _timeStepSeconds;
    }

    private DateTime GetTimeFromTimeStep(long timeStep)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timeStep * _timeStepSeconds).DateTime;
    }
}