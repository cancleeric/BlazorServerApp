namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// OTP 傳送服務介面 (SMS/Email)
/// 負責發送一次性密碼到使用者的手機或 Email
/// </summary>
public interface IOtpDeliveryService
{
    /// <summary>
    /// 發送 SMS OTP
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <param name="code">6位數 OTP 代碼</param>
    /// <param name="expirationMinutes">有效期限 (分鐘)</param>
    /// <returns>發送結果</returns>
    Task<OtpDeliveryResult> SendSmsOtpAsync(string phoneNumber, string code, int expirationMinutes = 5);

    /// <summary>
    /// 發送 Email OTP
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <param name="code">6位數 OTP 代碼</param>
    /// <param name="userName">使用者名稱</param>
    /// <param name="expirationMinutes">有效期限 (分鐘)</param>
    /// <returns>發送結果</returns>
    Task<OtpDeliveryResult> SendEmailOtpAsync(string emailAddress, string code, string userName, int expirationMinutes = 5);

    /// <summary>
    /// 產生 6 位數 OTP 代碼
    /// </summary>
    /// <returns>6位數數字代碼</returns>
    string GenerateOtpCode();

    /// <summary>
    /// 驗證手機號碼格式
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <returns>是否為有效格式</returns>
    bool ValidatePhoneNumber(string phoneNumber);

    /// <summary>
    /// 驗證 Email 地址格式
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <returns>是否為有效格式</returns>
    bool ValidateEmailAddress(string emailAddress);

    /// <summary>
    /// 遮蔽手機號碼 (用於顯示)
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <returns>遮蔽的手機號碼 (例: +886****1234)</returns>
    string MaskPhoneNumber(string phoneNumber);

    /// <summary>
    /// 遮蔽 Email 地址 (用於顯示)
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <returns>遮蔽的 Email 地址 (例: u***@example.com)</returns>
    string MaskEmailAddress(string emailAddress);
}

/// <summary>
/// OTP 傳送結果
/// </summary>
public class OtpDeliveryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DeliveryId { get; set; } // 第三方服務的傳送ID
    public string? MaskedTarget { get; set; } // 遮蔽的目標地址
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? ProviderName { get; set; } // Twilio, SendGrid 等
    public decimal? Cost { get; set; } // 傳送成本 (如果有的話)
}