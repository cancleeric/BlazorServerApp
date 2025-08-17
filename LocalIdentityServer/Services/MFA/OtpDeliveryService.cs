using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// OTP 傳送服務實作 (SMS/Email)
/// 支援 Twilio (SMS) 和 SendGrid (Email) 服務
/// </summary>
public class OtpDeliveryService : IOtpDeliveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OtpDeliveryService> _logger;
    private readonly string _twilioAccountSid;
    private readonly string _twilioAuthToken;
    private readonly string _twilioFromNumber;
    private readonly string _sendGridApiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public OtpDeliveryService(IConfiguration configuration, ILogger<OtpDeliveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Twilio 設定
        _twilioAccountSid = _configuration["Mfa:Twilio:AccountSid"] ?? "";
        _twilioAuthToken = _configuration["Mfa:Twilio:AuthToken"] ?? "";
        _twilioFromNumber = _configuration["Mfa:Twilio:FromNumber"] ?? "";

        // SendGrid 設定
        _sendGridApiKey = _configuration["Mfa:SendGrid:ApiKey"] ?? "";
        _fromEmail = _configuration["Mfa:Email:FromEmail"] ?? "";
        _fromName = _configuration["Mfa:Email:FromName"] ?? "LocalIdentityServer";

        // 初始化 Twilio
        if (!string.IsNullOrEmpty(_twilioAccountSid) && !string.IsNullOrEmpty(_twilioAuthToken))
        {
            TwilioClient.Init(_twilioAccountSid, _twilioAuthToken);
        }
    }

    public async Task<OtpDeliveryResult> SendSmsOtpAsync(string phoneNumber, string code, int expirationMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "Phone number cannot be null or empty"
            };
        }

        if (!ValidatePhoneNumber(phoneNumber))
        {
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "Invalid phone number format"
            };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "OTP code cannot be null or empty"
            };
        }

        try
        {
            // 檢查 Twilio 設定
            if (string.IsNullOrEmpty(_twilioAccountSid) || string.IsNullOrEmpty(_twilioAuthToken) || string.IsNullOrEmpty(_twilioFromNumber))
            {
                _logger.LogWarning("Twilio configuration is missing");
                return new OtpDeliveryResult
                {
                    Success = false,
                    ErrorMessage = "SMS service is not configured"
                };
            }

            var message = $"您的驗證碼是: {code}\n\n此驗證碼將在 {expirationMinutes} 分鐘後失效。\n\n如果您沒有要求此驗證碼，請忽略此訊息。";

            var twilioMessage = await MessageResource.CreateAsync(
                body: message,
                from: new Twilio.Types.PhoneNumber(_twilioFromNumber),
                to: new Twilio.Types.PhoneNumber(phoneNumber)
            );

            var result = new OtpDeliveryResult
            {
                Success = twilioMessage.Status != MessageResource.StatusEnum.Failed &&
                         twilioMessage.Status != MessageResource.StatusEnum.Undelivered,
                DeliveryId = twilioMessage.Sid,
                MaskedTarget = MaskPhoneNumber(phoneNumber),
                ProviderName = "Twilio",
                SentAt = DateTime.UtcNow
            };

            if (!result.Success)
            {
                result.ErrorMessage = $"SMS delivery failed with status: {twilioMessage.Status}";
                _logger.LogWarning("SMS delivery failed. Status: {Status}, SID: {Sid}", twilioMessage.Status, twilioMessage.Sid);
            }
            else
            {
                _logger.LogInformation("SMS OTP sent successfully. SID: {Sid}, To: {MaskedPhone}", 
                    twilioMessage.Sid, result.MaskedTarget);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS OTP to: {MaskedPhone}", MaskPhoneNumber(phoneNumber));
            
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "Failed to send SMS",
                MaskedTarget = MaskPhoneNumber(phoneNumber),
                ProviderName = "Twilio"
            };
        }
    }

    public async Task<OtpDeliveryResult> SendEmailOtpAsync(string emailAddress, string code, string userName, int expirationMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "Email address cannot be null or empty"
            };
        }

        if (!ValidateEmailAddress(emailAddress))
        {
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "Invalid email address format"
            };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "OTP code cannot be null or empty"
            };
        }

        try
        {
            // 檢查 SendGrid 設定
            if (string.IsNullOrEmpty(_sendGridApiKey) || string.IsNullOrEmpty(_fromEmail))
            {
                _logger.LogWarning("SendGrid configuration is missing");
                return new OtpDeliveryResult
                {
                    Success = false,
                    ErrorMessage = "Email service is not configured"
                };
            }

            var client = new SendGridClient(_sendGridApiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(emailAddress, userName);

            var subject = "您的多因子認證驗證碼";
            
            var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .code {{ font-size: 32px; font-weight: bold; color: #007bff; text-align: center; 
                background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; 
                   border-radius: 4px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>多因子認證驗證碼</h2>
        <p>親愛的 {userName}，</p>
        <p>您的多因子認證驗證碼是：</p>
        <div class='code'>{code}</div>
        <p>此驗證碼將在 <strong>{expirationMinutes} 分鐘</strong>後失效。</p>
        <div class='warning'>
            <strong>安全提醒：</strong>
            <ul>
                <li>請勿與任何人分享此驗證碼</li>
                <li>如果您沒有要求此驗證碼，請立即聯絡管理員</li>
                <li>此郵件為系統自動發送，請勿回覆</li>
            </ul>
        </div>
        <p>謝謝您的使用。</p>
        <p><em>LocalIdentityServer 團隊</em></p>
    </div>
</body>
</html>";

            var plainTextContent = $@"
多因子認證驗證碼

親愛的 {userName}，

您的多因子認證驗證碼是：{code}

此驗證碼將在 {expirationMinutes} 分鐘後失效。

安全提醒：
- 請勿與任何人分享此驗證碼
- 如果您沒有要求此驗證碼，請立即聯絡管理員
- 此郵件為系統自動發送，請勿回覆

謝謝您的使用。

LocalIdentityServer 團隊";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = await client.SendEmailAsync(msg);

            var result = new OtpDeliveryResult
            {
                Success = response.IsSuccessStatusCode,
                DeliveryId = response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault(),
                MaskedTarget = MaskEmailAddress(emailAddress),
                ProviderName = "SendGrid",
                SentAt = DateTime.UtcNow
            };

            if (!result.Success)
            {
                result.ErrorMessage = $"Email delivery failed with status: {response.StatusCode}";
                _logger.LogWarning("Email delivery failed. Status: {Status}, To: {MaskedEmail}", 
                    response.StatusCode, result.MaskedTarget);
            }
            else
            {
                _logger.LogInformation("Email OTP sent successfully. MessageId: {MessageId}, To: {MaskedEmail}", 
                    result.DeliveryId, result.MaskedTarget);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Email OTP to: {MaskedEmail}", MaskEmailAddress(emailAddress));
            
            return new OtpDeliveryResult
            {
                Success = false,
                ErrorMessage = "Failed to send email",
                MaskedTarget = MaskEmailAddress(emailAddress),
                ProviderName = "SendGrid"
            };
        }
    }

    public string GenerateOtpCode()
    {
        try
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            var randomNumber = BitConverter.ToUInt32(bytes, 0);
            var code = (randomNumber % 1000000).ToString("D6");
            
            _logger.LogDebug("Generated 6-digit OTP code");
            
            return code;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate OTP code");
            throw new InvalidOperationException("Failed to generate OTP code", ex);
        }
    }

    public bool ValidatePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // 支援國際格式 (+886912345678) 和本地格式 (0912345678)
        var phoneRegex = new Regex(@"^(\+\d{1,3}[- ]?)?\d{8,15}$");
        return phoneRegex.IsMatch(phoneNumber.Replace(" ", "").Replace("-", ""));
    }

    public bool ValidateEmailAddress(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            return false;

        try
        {
            var email = new System.Net.Mail.MailAddress(emailAddress);
            return email.Address == emailAddress;
        }
        catch
        {
            return false;
        }
    }

    public string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return "";

        var cleanNumber = phoneNumber.Replace(" ", "").Replace("-", "");
        
        if (cleanNumber.StartsWith("+"))
        {
            // 國際格式: +886****1234
            if (cleanNumber.Length >= 8)
            {
                var countryCode = cleanNumber.Substring(0, Math.Min(4, cleanNumber.Length - 4));
                var lastFour = cleanNumber.Substring(cleanNumber.Length - 4);
                return $"{countryCode}****{lastFour}";
            }
        }
        else if (cleanNumber.Length >= 8)
        {
            // 本地格式: 09****1234
            var prefix = cleanNumber.Substring(0, 2);
            var lastFour = cleanNumber.Substring(cleanNumber.Length - 4);
            return $"{prefix}****{lastFour}";
        }

        return "****";
    }

    public string MaskEmailAddress(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            return "";

        try
        {
            var atIndex = emailAddress.IndexOf('@');
            if (atIndex <= 0)
                return "****";

            var localPart = emailAddress.Substring(0, atIndex);
            var domainPart = emailAddress.Substring(atIndex);

            if (localPart.Length <= 2)
            {
                return $"****{domainPart}";
            }
            else
            {
                var firstChar = localPart[0];
                var lastChar = localPart[localPart.Length - 1];
                return $"{firstChar}***{lastChar}{domainPart}";
            }
        }
        catch
        {
            return "****";
        }
    }
}