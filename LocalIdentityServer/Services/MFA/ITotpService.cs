namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// TOTP (Time-based One-Time Password) 服務介面 - 遵循介面隔離原則 (ISP)
/// 負責 RFC 6238 標準的 TOTP 實作，支援 Google Authenticator 與 Microsoft Authenticator
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// 產生新的 TOTP 密鑰
    /// </summary>
    /// <returns>Base32 編碼的密鑰</returns>
    string GenerateSecret();

    /// <summary>
    /// 產生 TOTP URI (用於 QR Code)
    /// </summary>
    /// <param name="userIdentifier">使用者識別碼 (email 或 username)</param>
    /// <param name="secret">TOTP 密鑰</param>
    /// <param name="issuer">發行者名稱</param>
    /// <param name="accountName">帳戶名稱</param>
    /// <returns>otpauth:// 格式的 URI</returns>
    string GenerateTotpUri(string userIdentifier, string secret, string issuer = "LocalIdentityServer", string? accountName = null);

    /// <summary>
    /// 驗證 TOTP 代碼
    /// </summary>
    /// <param name="secret">TOTP 密鑰</param>
    /// <param name="code">使用者輸入的 6 位數代碼</param>
    /// <param name="timeWindow">時間窗口 (預設 30 秒)</param>
    /// <returns>驗證結果</returns>
    TotpVerificationResult VerifyTotp(string secret, string code, int timeWindow = 30);

    /// <summary>
    /// 驗證 TOTP 代碼 (支援時間容錯)
    /// </summary>
    /// <param name="secret">TOTP 密鑰</param>
    /// <param name="code">使用者輸入的代碼</param>
    /// <param name="toleranceSteps">容錯步數 (預設 1 步 = ±30秒)</param>
    /// <returns>驗證結果</returns>
    TotpVerificationResult VerifyTotpWithTolerance(string secret, string code, int toleranceSteps = 1);

    /// <summary>
    /// 取得當前 TOTP 代碼 (用於測試)
    /// </summary>
    /// <param name="secret">TOTP 密鑰</param>
    /// <returns>當前 6 位數代碼</returns>
    string GetCurrentTotp(string secret);

    /// <summary>
    /// 取得剩餘有效時間 (秒)
    /// </summary>
    /// <param name="timeWindow">時間窗口</param>
    /// <returns>剩餘秒數</returns>
    int GetRemainingTime(int timeWindow = 30);

    /// <summary>
    /// 驗證密鑰格式是否有效
    /// </summary>
    /// <param name="secret">TOTP 密鑰</param>
    /// <returns>是否有效</returns>
    bool IsValidSecret(string secret);
}

/// <summary>
/// TOTP 驗證結果
/// </summary>
public class TotpVerificationResult
{
    /// <summary>
    /// 驗證是否成功
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 使用的時間步驟 (0 為當前時間，±1 為前後時間窗口)
    /// </summary>
    public int TimeStep { get; set; }

    /// <summary>
    /// 驗證時間
    /// </summary>
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 錯誤訊息 (驗證失敗時)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 代碼是否已被使用過 (防重放攻擊)
    /// </summary>
    public bool IsReplayAttack { get; set; }

    /// <summary>
    /// 剩餘有效時間 (秒)
    /// </summary>
    public int RemainingTime { get; set; }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static TotpVerificationResult Success(int timeStep = 0, int remainingTime = 0)
    {
        return new TotpVerificationResult
        {
            IsValid = true,
            TimeStep = timeStep,
            RemainingTime = remainingTime
        };
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    public static TotpVerificationResult Failure(string errorMessage, bool isReplayAttack = false)
    {
        return new TotpVerificationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            IsReplayAttack = isReplayAttack
        };
    }
}