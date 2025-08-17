namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// Email 服務介面 - 遵循介面隔離原則 (ISP)
/// 負責發送 MFA 驗證碼和通知郵件
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// 發送 MFA 驗證碼
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <param name="code">6位數驗證碼</param>
    /// <param name="expiryMinutes">驗證碼有效期限 (分鐘)</param>
    /// <param name="userName">使用者名稱</param>
    /// <returns>發送結果</returns>
    Task<EmailResult> SendMfaCodeAsync(string emailAddress, string code, int expiryMinutes = 5, string? userName = null);

    /// <summary>
    /// 發送 MFA 設定完成通知
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <param name="userName">使用者名稱</param>
    /// <param name="deviceName">設備名稱</param>
    /// <param name="mfaMethod">MFA 方法</param>
    /// <returns>發送結果</returns>
    Task<EmailResult> SendMfaSetupNotificationAsync(string emailAddress, string? userName, string deviceName, string mfaMethod);

    /// <summary>
    /// 發送 MFA 停用通知
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <param name="userName">使用者名稱</param>
    /// <param name="disabledMethod">被停用的 MFA 方法</param>
    /// <returns>發送結果</returns>
    Task<EmailResult> SendMfaDisabledNotificationAsync(string emailAddress, string? userName, string disabledMethod);

    /// <summary>
    /// 發送安全警報
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <param name="userName">使用者名稱</param>
    /// <param name="alertType">警報類型</param>
    /// <param name="details">詳細資訊</param>
    /// <param name="ipAddress">IP 地址</param>
    /// <param name="userAgent">User Agent</param>
    /// <returns>發送結果</returns>
    Task<EmailResult> SendSecurityAlertAsync(string emailAddress, string? userName, string alertType, string details, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// 發送備用代碼
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <param name="userName">使用者名稱</param>
    /// <param name="backupCodes">備用代碼列表</param>
    /// <returns>發送結果</returns>
    Task<EmailResult> SendBackupCodesAsync(string emailAddress, string? userName, IEnumerable<string> backupCodes);

    /// <summary>
    /// 驗證 Email 地址格式
    /// </summary>
    /// <param name="emailAddress">Email 地址</param>
    /// <returns>是否有效</returns>
    bool IsValidEmailAddress(string emailAddress);

    /// <summary>
    /// 遮罩 Email 地址以保護隱私
    /// </summary>
    /// <param name="emailAddress">原始 Email 地址</param>
    /// <returns>遮罩後的 Email 地址</returns>
    string MaskEmailAddress(string emailAddress);
}

/// <summary>
/// Email 發送結果
/// </summary>
public class EmailResult
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
    /// Email 地址 (已遮罩)
    /// </summary>
    public string? MaskedEmailAddress { get; set; }

    /// <summary>
    /// Email 主旨
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static EmailResult Success(string messageId, string maskedEmail, string subject)
    {
        return new EmailResult
        {
            IsSuccess = true,
            MessageId = messageId,
            MaskedEmailAddress = maskedEmail,
            Subject = subject
        };
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    public static EmailResult Failure(string errorMessage, string? providerResponse = null)
    {
        return new EmailResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ProviderResponse = providerResponse
        };
    }
}