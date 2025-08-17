namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// SMS 服務介面 - 遵循介面隔離原則 (ISP)
/// 負責發送 MFA 驗證碼和通知訊息
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// 發送 MFA 驗證碼
    /// </summary>
    /// <param name="phoneNumber">手機號碼 (國際格式，例如 +886912345678)</param>
    /// <param name="code">6位數驗證碼</param>
    /// <param name="expiryMinutes">驗證碼有效期限 (分鐘)</param>
    /// <returns>發送結果</returns>
    Task<SmsResult> SendMfaCodeAsync(string phoneNumber, string code, int expiryMinutes = 5);

    /// <summary>
    /// 發送 MFA 設定通知
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <param name="deviceName">設備名稱</param>
    /// <returns>發送結果</returns>
    Task<SmsResult> SendMfaSetupNotificationAsync(string phoneNumber, string deviceName);

    /// <summary>
    /// 發送安全警報
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <param name="alertType">警報類型</param>
    /// <param name="details">詳細資訊</param>
    /// <returns>發送結果</returns>
    Task<SmsResult> SendSecurityAlertAsync(string phoneNumber, string alertType, string details);

    /// <summary>
    /// 驗證手機號碼格式
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <returns>是否有效</returns>
    bool IsValidPhoneNumber(string phoneNumber);

    /// <summary>
    /// 格式化手機號碼為國際格式
    /// </summary>
    /// <param name="phoneNumber">手機號碼</param>
    /// <param name="defaultCountryCode">預設國家代碼 (例如 +886)</param>
    /// <returns>國際格式手機號碼</returns>
    string FormatPhoneNumber(string phoneNumber, string defaultCountryCode = "+886");
}

/// <summary>
/// SMS 發送結果
/// </summary>
public class SmsResult
{
    /// <summary>
    /// 是否發送成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 訊息ID (用於追蹤)
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 供應商回應
    /// </summary>
    public string? ProviderResponse { get; set; }

    /// <summary>
    /// 發送時間
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 手機號碼 (已遮罩)
    /// </summary>
    public string? MaskedPhoneNumber { get; set; }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static SmsResult Success(string messageId, string maskedPhone)
    {
        return new SmsResult
        {
            IsSuccess = true,
            MessageId = messageId,
            MaskedPhoneNumber = maskedPhone
        };
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    public static SmsResult Failure(string errorMessage, string? providerResponse = null)
    {
        return new SmsResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ProviderResponse = providerResponse
        };
    }
}