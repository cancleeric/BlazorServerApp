using System.Text.RegularExpressions;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// SMS 服務實作 - 使用 Twilio 服務
/// 遵循單一責任原則 (SRP) - 專責處理 SMS 發送
/// 遵循依賴反轉原則 (DIP) - 透過設定注入依賴
/// </summary>
public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromPhoneNumber;
    private readonly bool _isEnabled;
    private readonly string _serviceName;

    public SmsService(ILogger<SmsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // 從設定檔讀取 Twilio 設定
        _accountSid = _configuration["Mfa:Sms:Twilio:AccountSid"] ?? string.Empty;
        _authToken = _configuration["Mfa:Sms:Twilio:AuthToken"] ?? string.Empty;
        _fromPhoneNumber = _configuration["Mfa:Sms:Twilio:FromPhoneNumber"] ?? string.Empty;
        _isEnabled = bool.Parse(_configuration["Mfa:Sms:Enabled"] ?? "false");
        _serviceName = _configuration["Mfa:Sms:ServiceName"] ?? "LocalIdentityServer";

        // 初始化 Twilio 客戶端
        if (_isEnabled && !string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken))
        {
            TwilioClient.Init(_accountSid, _authToken);
            _logger.LogInformation("Twilio SMS service initialized successfully");
        }
        else
        {
            _logger.LogWarning("SMS service is disabled or not configured properly");
        }
    }

    public async Task<SmsResult> SendMfaCodeAsync(string phoneNumber, string code, int expiryMinutes = 5)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("SMS service is disabled");
            return SmsResult.Failure("SMS 服務已停用");
        }

        if (!IsValidPhoneNumber(phoneNumber))
        {
            _logger.LogWarning("Invalid phone number format: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return SmsResult.Failure("手機號碼格式無效");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
        {
            _logger.LogWarning("Invalid MFA code format");
            return SmsResult.Failure("驗證碼格式無效");
        }

        try
        {
            var formattedPhone = FormatPhoneNumber(phoneNumber);
            var message = $"您的 {_serviceName} 驗證碼是: {code}\n" +
                         $"此驗證碼將在 {expiryMinutes} 分鐘後過期。\n" +
                         $"如果您沒有要求此驗證碼，請忽略此訊息。";

            var messageResource = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_fromPhoneNumber),
                to: new PhoneNumber(formattedPhone)
            );

            _logger.LogInformation("MFA code sent successfully. MessageSid: {MessageSid}, Phone: {Phone}", 
                messageResource.Sid, MaskPhoneNumber(formattedPhone));

            return SmsResult.Success(messageResource.Sid, MaskPhoneNumber(formattedPhone));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send MFA code to phone: {Phone}", MaskPhoneNumber(phoneNumber));
            return SmsResult.Failure("發送驗證碼失敗", ex.Message);
        }
    }

    public async Task<SmsResult> SendMfaSetupNotificationAsync(string phoneNumber, string deviceName)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("SMS service is disabled");
            return SmsResult.Failure("SMS 服務已停用");
        }

        if (!IsValidPhoneNumber(phoneNumber))
        {
            _logger.LogWarning("Invalid phone number format: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return SmsResult.Failure("手機號碼格式無效");
        }

        try
        {
            var formattedPhone = FormatPhoneNumber(phoneNumber);
            var message = $"您的 {_serviceName} 帳戶已在設備 \"{deviceName}\" 上設定多因子認證 (MFA)。\n" +
                         $"設定時間: {DateTime.Now:yyyy/MM/dd HH:mm}\n" +
                         $"如果這不是您的操作，請立即聯繫客服。";

            var messageResource = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_fromPhoneNumber),
                to: new PhoneNumber(formattedPhone)
            );

            _logger.LogInformation("MFA setup notification sent successfully. MessageSid: {MessageSid}, Phone: {Phone}", 
                messageResource.Sid, MaskPhoneNumber(formattedPhone));

            return SmsResult.Success(messageResource.Sid, MaskPhoneNumber(formattedPhone));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send MFA setup notification to phone: {Phone}", MaskPhoneNumber(phoneNumber));
            return SmsResult.Failure("發送設定通知失敗", ex.Message);
        }
    }

    public async Task<SmsResult> SendSecurityAlertAsync(string phoneNumber, string alertType, string details)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("SMS service is disabled");
            return SmsResult.Failure("SMS 服務已停用");
        }

        if (!IsValidPhoneNumber(phoneNumber))
        {
            _logger.LogWarning("Invalid phone number format: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return SmsResult.Failure("手機號碼格式無效");
        }

        try
        {
            var formattedPhone = FormatPhoneNumber(phoneNumber);
            var message = $"【{_serviceName} 安全警報】\n" +
                         $"警報類型: {alertType}\n" +
                         $"詳細資訊: {details}\n" +
                         $"時間: {DateTime.Now:yyyy/MM/dd HH:mm}\n" +
                         $"如有疑問請立即聯繫客服。";

            var messageResource = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_fromPhoneNumber),
                to: new PhoneNumber(formattedPhone)
            );

            _logger.LogInformation("Security alert sent successfully. MessageSid: {MessageSid}, Phone: {Phone}, AlertType: {AlertType}", 
                messageResource.Sid, MaskPhoneNumber(formattedPhone), alertType);

            return SmsResult.Success(messageResource.Sid, MaskPhoneNumber(formattedPhone));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security alert to phone: {Phone}, AlertType: {AlertType}", 
                MaskPhoneNumber(phoneNumber), alertType);
            return SmsResult.Failure("發送安全警報失敗", ex.Message);
        }
    }

    public bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // 移除所有非數字字符，除了 + 號
        var cleanPhone = Regex.Replace(phoneNumber, @"[^\d+]", "");

        // 檢查國際格式 (+886XXXXXXXXX 或 +1XXXXXXXXXX 等)
        var internationalPattern = @"^\+\d{10,15}$";
        if (Regex.IsMatch(cleanPhone, internationalPattern))
            return true;

        // 檢查台灣手機號碼格式 (09XXXXXXXX)
        var taiwanMobilePattern = @"^09\d{8}$";
        if (Regex.IsMatch(cleanPhone, taiwanMobilePattern))
            return true;

        return false;
    }

    public string FormatPhoneNumber(string phoneNumber, string defaultCountryCode = "+886")
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("手機號碼不可為空", nameof(phoneNumber));

        // 移除所有非數字字符，除了 + 號
        var cleanPhone = Regex.Replace(phoneNumber, @"[^\d+]", "");

        // 如果已經是國際格式，直接返回
        if (cleanPhone.StartsWith("+"))
            return cleanPhone;

        // 處理台灣手機號碼 (09XXXXXXXX -> +8869XXXXXXXX)
        if (cleanPhone.StartsWith("09") && cleanPhone.Length == 10)
        {
            return "+886" + cleanPhone.Substring(1);
        }

        // 處理其他格式，加上預設國家代碼
        if (cleanPhone.Length >= 9 && cleanPhone.Length <= 15)
        {
            return defaultCountryCode + cleanPhone;
        }

        throw new ArgumentException("手機號碼格式無效", nameof(phoneNumber));
    }

    /// <summary>
    /// 遮罩手機號碼以保護隱私
    /// </summary>
    /// <param name="phoneNumber">原始手機號碼</param>
    /// <returns>遮罩後的手機號碼</returns>
    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length <= 4)
            return "***";

        // 保留前3位和後2位，中間用 * 遮罩
        var start = phoneNumber.Substring(0, Math.Min(3, phoneNumber.Length));
        var end = phoneNumber.Length > 5 ? phoneNumber.Substring(phoneNumber.Length - 2) : "";
        var maskedLength = Math.Max(0, phoneNumber.Length - 5);
        
        return $"{start}{'*'.ToString().PadLeft(maskedLength, '*')}{end}";
    }
}