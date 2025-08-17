using System.Net.Mail;
using System.Text.RegularExpressions;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// Email 服務實作 - 使用 SendGrid 服務
/// 遵循單一責任原則 (SRP) - 專責處理 Email 發送
/// 遵循依賴反轉原則 (DIP) - 透過設定注入依賴
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ISendGridClient _sendGridClient;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _isEnabled;
    private readonly string _serviceName;
    private readonly string _supportEmail;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // 從設定檔讀取 SendGrid 設定
        var apiKey = _configuration["Mfa:Email:SendGrid:ApiKey"] ?? string.Empty;
        _fromEmail = _configuration["Mfa:Email:SendGrid:FromEmail"] ?? "noreply@localhost";
        _fromName = _configuration["Mfa:Email:SendGrid:FromName"] ?? "LocalIdentityServer";
        _isEnabled = bool.Parse(_configuration["Mfa:Email:Enabled"] ?? "false");
        _serviceName = _configuration["Mfa:Email:ServiceName"] ?? "LocalIdentityServer";
        _supportEmail = _configuration["Mfa:Email:SupportEmail"] ?? "support@localhost";

        // 初始化 SendGrid 客戶端
        if (_isEnabled && !string.IsNullOrEmpty(apiKey))
        {
            _sendGridClient = new SendGridClient(apiKey);
            _logger.LogInformation("SendGrid email service initialized successfully");
        }
        else
        {
            _logger.LogWarning("Email service is disabled or not configured properly");
            _sendGridClient = null!;
        }
    }

    public async Task<EmailResult> SendMfaCodeAsync(string emailAddress, string code, int expiryMinutes = 5, string? userName = null)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled");
            return EmailResult.Failure("Email 服務已停用");
        }

        if (!IsValidEmailAddress(emailAddress))
        {
            _logger.LogWarning("Invalid email address format: {Email}", MaskEmailAddress(emailAddress));
            return EmailResult.Failure("Email 地址格式無效");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
        {
            _logger.LogWarning("Invalid MFA code format");
            return EmailResult.Failure("驗證碼格式無效");
        }

        try
        {
            var subject = $"您的 {_serviceName} 驗證碼";
            var displayName = string.IsNullOrWhiteSpace(userName) ? "用戶" : userName;
            
            var htmlContent = GenerateMfaCodeEmailHtml(displayName, code, expiryMinutes);
            var textContent = GenerateMfaCodeEmailText(displayName, code, expiryMinutes);

            var response = await SendEmailAsync(emailAddress, subject, textContent, htmlContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("MFA code email sent successfully to: {Email}", MaskEmailAddress(emailAddress));
                return EmailResult.Success(response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault() ?? Guid.NewGuid().ToString(), 
                    MaskEmailAddress(emailAddress), subject);
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                _logger.LogError("Failed to send MFA code email. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorBody);
                return EmailResult.Failure("發送驗證碼 Email 失敗", errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending MFA code email to: {Email}", MaskEmailAddress(emailAddress));
            return EmailResult.Failure("發送驗證碼 Email 時發生錯誤", ex.Message);
        }
    }

    public async Task<EmailResult> SendMfaSetupNotificationAsync(string emailAddress, string? userName, string deviceName, string mfaMethod)
    {
        if (!_isEnabled)
            return EmailResult.Failure("Email 服務已停用");

        if (!IsValidEmailAddress(emailAddress))
            return EmailResult.Failure("Email 地址格式無效");

        try
        {
            var subject = $"{_serviceName} - MFA 多因子認證已設定";
            var displayName = string.IsNullOrWhiteSpace(userName) ? "用戶" : userName;
            
            var htmlContent = GenerateMfaSetupNotificationHtml(displayName, deviceName, mfaMethod);
            var textContent = GenerateMfaSetupNotificationText(displayName, deviceName, mfaMethod);

            var response = await SendEmailAsync(emailAddress, subject, textContent, htmlContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("MFA setup notification sent successfully to: {Email}", MaskEmailAddress(emailAddress));
                return EmailResult.Success(response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault() ?? Guid.NewGuid().ToString(), 
                    MaskEmailAddress(emailAddress), subject);
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                return EmailResult.Failure("發送 MFA 設定通知失敗", errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending MFA setup notification to: {Email}", MaskEmailAddress(emailAddress));
            return EmailResult.Failure("發送 MFA 設定通知時發生錯誤", ex.Message);
        }
    }

    public async Task<EmailResult> SendMfaDisabledNotificationAsync(string emailAddress, string? userName, string disabledMethod)
    {
        if (!_isEnabled)
            return EmailResult.Failure("Email 服務已停用");

        if (!IsValidEmailAddress(emailAddress))
            return EmailResult.Failure("Email 地址格式無效");

        try
        {
            var subject = $"{_serviceName} - MFA 多因子認證已停用";
            var displayName = string.IsNullOrWhiteSpace(userName) ? "用戶" : userName;
            
            var htmlContent = GenerateMfaDisabledNotificationHtml(displayName, disabledMethod);
            var textContent = GenerateMfaDisabledNotificationText(displayName, disabledMethod);

            var response = await SendEmailAsync(emailAddress, subject, textContent, htmlContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("MFA disabled notification sent successfully to: {Email}", MaskEmailAddress(emailAddress));
                return EmailResult.Success(response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault() ?? Guid.NewGuid().ToString(), 
                    MaskEmailAddress(emailAddress), subject);
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                return EmailResult.Failure("發送 MFA 停用通知失敗", errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending MFA disabled notification to: {Email}", MaskEmailAddress(emailAddress));
            return EmailResult.Failure("發送 MFA 停用通知時發生錯誤", ex.Message);
        }
    }

    public async Task<EmailResult> SendSecurityAlertAsync(string emailAddress, string? userName, string alertType, string details, string? ipAddress = null, string? userAgent = null)
    {
        if (!_isEnabled)
            return EmailResult.Failure("Email 服務已停用");

        if (!IsValidEmailAddress(emailAddress))
            return EmailResult.Failure("Email 地址格式無效");

        try
        {
            var subject = $"🚨 {_serviceName} - 安全警報通知";
            var displayName = string.IsNullOrWhiteSpace(userName) ? "用戶" : userName;
            
            var htmlContent = GenerateSecurityAlertHtml(displayName, alertType, details, ipAddress, userAgent);
            var textContent = GenerateSecurityAlertText(displayName, alertType, details, ipAddress, userAgent);

            var response = await SendEmailAsync(emailAddress, subject, textContent, htmlContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Security alert sent successfully to: {Email}, AlertType: {AlertType}", 
                    MaskEmailAddress(emailAddress), alertType);
                return EmailResult.Success(response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault() ?? Guid.NewGuid().ToString(), 
                    MaskEmailAddress(emailAddress), subject);
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                return EmailResult.Failure("發送安全警報失敗", errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending security alert to: {Email}", MaskEmailAddress(emailAddress));
            return EmailResult.Failure("發送安全警報時發生錯誤", ex.Message);
        }
    }

    public async Task<EmailResult> SendBackupCodesAsync(string emailAddress, string? userName, IEnumerable<string> backupCodes)
    {
        if (!_isEnabled)
            return EmailResult.Failure("Email 服務已停用");

        if (!IsValidEmailAddress(emailAddress))
            return EmailResult.Failure("Email 地址格式無效");

        var codes = backupCodes.ToList();
        if (!codes.Any())
            return EmailResult.Failure("備用代碼列表為空");

        try
        {
            var subject = $"{_serviceName} - MFA 備用代碼";
            var displayName = string.IsNullOrWhiteSpace(userName) ? "用戶" : userName;
            
            var htmlContent = GenerateBackupCodesHtml(displayName, codes);
            var textContent = GenerateBackupCodesText(displayName, codes);

            var response = await SendEmailAsync(emailAddress, subject, textContent, htmlContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Backup codes sent successfully to: {Email}", MaskEmailAddress(emailAddress));
                return EmailResult.Success(response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault() ?? Guid.NewGuid().ToString(), 
                    MaskEmailAddress(emailAddress), subject);
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                return EmailResult.Failure("發送備用代碼失敗", errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending backup codes to: {Email}", MaskEmailAddress(emailAddress));
            return EmailResult.Failure("發送備用代碼時發生錯誤", ex.Message);
        }
    }

    public bool IsValidEmailAddress(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
            return false;

        try
        {
            var mailAddress = new MailAddress(emailAddress);
            return mailAddress.Address == emailAddress;
        }
        catch
        {
            return false;
        }
    }

    public string MaskEmailAddress(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress) || !emailAddress.Contains("@"))
            return "***@***.***";

        var parts = emailAddress.Split('@');
        if (parts.Length != 2)
            return "***@***.***";

        var localPart = parts[0];
        var domainPart = parts[1];

        // 遮罩本地部分
        var maskedLocal = localPart.Length <= 2 
            ? new string('*', localPart.Length)
            : $"{localPart[0]}{'*'.ToString().PadLeft(localPart.Length - 2, '*')}{localPart[^1]}";

        // 遮罩域名部分
        var domainParts = domainPart.Split('.');
        var maskedDomain = domainParts.Length > 1
            ? $"***{domainPart.Substring(domainPart.LastIndexOf('.'))}"
            : "***";

        return $"{maskedLocal}@{maskedDomain}";
    }

    private async Task<Response> SendEmailAsync(string toEmail, string subject, string textContent, string htmlContent)
    {
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, textContent, htmlContent);

        // 設定類別以便於追蹤
        msg.Categories = new List<string> { "MFA", "Authentication", "Security" };

        return await _sendGridClient.SendEmailAsync(msg);
    }

    private string GenerateMfaCodeEmailHtml(string userName, string code, int expiryMinutes)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>MFA 驗證碼</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .code {{ font-size: 24px; font-weight: bold; color: #007bff; text-align: center; margin: 20px 0; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{_serviceName}</h1>
            <h2>多因子認證驗證碼</h2>
        </div>
        <div class=""content"">
            <p>親愛的 {userName}，</p>
            <p>您的多因子認證驗證碼如下：</p>
            <div class=""code"">{code}</div>
            <p><strong>此驗證碼將在 {expiryMinutes} 分鐘後過期。</strong></p>
            <p>如果您沒有要求此驗證碼，請忽略此郵件並聯繫我們的客服團隊。</p>
            <p>為了您的帳戶安全，請勿將此驗證碼分享給他人。</p>
        </div>
        <div class=""footer"">
            <p>{_serviceName} 安全團隊</p>
            <p>如有疑問，請聯繫：{_supportEmail}</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateMfaCodeEmailText(string userName, string code, int expiryMinutes)
    {
        return $@"
{_serviceName} - 多因子認證驗證碼

親愛的 {userName}，

您的多因子認證驗證碼是：{code}

此驗證碼將在 {expiryMinutes} 分鐘後過期。

如果您沒有要求此驗證碼，請忽略此郵件並聯繫我們的客服團隊。
為了您的帳戶安全，請勿將此驗證碼分享給他人。

{_serviceName} 安全團隊
如有疑問，請聯繫：{_supportEmail}";
    }

    private string GenerateMfaSetupNotificationHtml(string userName, string deviceName, string mfaMethod)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>MFA 設定完成</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .info {{ background-color: #e9ecef; padding: 15px; margin: 15px 0; border-radius: 5px; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{_serviceName}</h1>
            <h2>✅ MFA 多因子認證已設定</h2>
        </div>
        <div class=""content"">
            <p>親愛的 {userName}，</p>
            <p>您的帳戶已成功設定多因子認證 (MFA)。</p>
            <div class=""info"">
                <p><strong>設定詳情：</strong></p>
                <ul>
                    <li>認證方法：{mfaMethod}</li>
                    <li>設備名稱：{deviceName}</li>
                    <li>設定時間：{DateTime.Now:yyyy年MM月dd日 HH:mm}</li>
                </ul>
            </div>
            <p>多因子認證將為您的帳戶提供額外的安全保護。</p>
            <p>如果這不是您的操作，請立即聯繫我們的客服團隊。</p>
        </div>
        <div class=""footer"">
            <p>{_serviceName} 安全團隊</p>
            <p>如有疑問，請聯繫：{_supportEmail}</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateMfaSetupNotificationText(string userName, string deviceName, string mfaMethod)
    {
        return $@"
{_serviceName} - MFA 多因子認證已設定

親愛的 {userName}，

您的帳戶已成功設定多因子認證 (MFA)。

設定詳情：
- 認證方法：{mfaMethod}
- 設備名稱：{deviceName}
- 設定時間：{DateTime.Now:yyyy年MM月dd日 HH:mm}

多因子認證將為您的帳戶提供額外的安全保護。

如果這不是您的操作，請立即聯繫我們的客服團隊。

{_serviceName} 安全團隊
如有疑問，請聯繫：{_supportEmail}";
    }

    private string GenerateMfaDisabledNotificationHtml(string userName, string disabledMethod)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>MFA 已停用</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ffc107; color: #212529; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; margin: 15px 0; border-radius: 5px; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{_serviceName}</h1>
            <h2>⚠️ MFA 多因子認證已停用</h2>
        </div>
        <div class=""content"">
            <p>親愛的 {userName}，</p>
            <p>您的帳戶的多因子認證功能已被停用。</p>
            <div class=""warning"">
                <p><strong>停用詳情：</strong></p>
                <ul>
                    <li>停用方法：{disabledMethod}</li>
                    <li>停用時間：{DateTime.Now:yyyy年MM月dd日 HH:mm}</li>
                </ul>
            </div>
            <p><strong>重要提醒：</strong>停用多因子認證會降低您帳戶的安全性。建議您儘快重新啟用 MFA 功能。</p>
            <p>如果這不是您的操作，請立即聯繫我們的客服團隊。</p>
        </div>
        <div class=""footer"">
            <p>{_serviceName} 安全團隊</p>
            <p>如有疑問，請聯繫：{_supportEmail}</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateMfaDisabledNotificationText(string userName, string disabledMethod)
    {
        return $@"
{_serviceName} - MFA 多因子認證已停用

親愛的 {userName}，

您的帳戶的多因子認證功能已被停用。

停用詳情：
- 停用方法：{disabledMethod}
- 停用時間：{DateTime.Now:yyyy年MM月dd日 HH:mm}

⚠️ 重要提醒：停用多因子認證會降低您帳戶的安全性。建議您儘快重新啟用 MFA 功能。

如果這不是您的操作，請立即聯繫我們的客服團隊。

{_serviceName} 安全團隊
如有疑問，請聯繫：{_supportEmail}";
    }

    private string GenerateSecurityAlertHtml(string userName, string alertType, string details, string? ipAddress, string? userAgent)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>安全警報</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .alert {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; margin: 15px 0; border-radius: 5px; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🚨 {_serviceName}</h1>
            <h2>安全警報通知</h2>
        </div>
        <div class=""content"">
            <p>親愛的 {userName}，</p>
            <p>我們偵測到您的帳戶有異常活動，請立即檢查。</p>
            <div class=""alert"">
                <p><strong>警報詳情：</strong></p>
                <ul>
                    <li>警報類型：{alertType}</li>
                    <li>詳細說明：{details}</li>
                    <li>發生時間：{DateTime.Now:yyyy年MM月dd日 HH:mm}</li>
                    {(string.IsNullOrEmpty(ipAddress) ? "" : $"<li>IP 地址：{ipAddress}</li>")}
                    {(string.IsNullOrEmpty(userAgent) ? "" : $"<li>設備資訊：{userAgent}</li>")}
                </ul>
            </div>
            <p><strong>建議行動：</strong></p>
            <ul>
                <li>立即檢查您的帳戶活動</li>
                <li>如發現異常，請立即修改密碼</li>
                <li>確認多因子認證設定正常</li>
                <li>如有疑問，請聯繫客服團隊</li>
            </ul>
        </div>
        <div class=""footer"">
            <p>{_serviceName} 安全團隊</p>
            <p>如有疑問，請聯繫：{_supportEmail}</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateSecurityAlertText(string userName, string alertType, string details, string? ipAddress, string? userAgent)
    {
        return $@"
🚨 {_serviceName} - 安全警報通知

親愛的 {userName}，

我們偵測到您的帳戶有異常活動，請立即檢查。

警報詳情：
- 警報類型：{alertType}
- 詳細說明：{details}
- 發生時間：{DateTime.Now:yyyy年MM月dd日 HH:mm}
{(string.IsNullOrEmpty(ipAddress) ? "" : $"- IP 地址：{ipAddress}")}
{(string.IsNullOrEmpty(userAgent) ? "" : $"- 設備資訊：{userAgent}")}

建議行動：
- 立即檢查您的帳戶活動
- 如發現異常，請立即修改密碼
- 確認多因子認證設定正常
- 如有疑問，請聯繫客服團隊

{_serviceName} 安全團隊
如有疑問，請聯繫：{_supportEmail}";
    }

    private string GenerateBackupCodesHtml(string userName, List<string> backupCodes)
    {
        var codesList = string.Join("", backupCodes.Select(code => $"<li><code>{code}</code></li>"));
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>MFA 備用代碼</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .codes {{ background-color: #e9ecef; padding: 20px; margin: 15px 0; border-radius: 5px; }}
        .codes ul {{ list-style-type: none; padding: 0; }}
        .codes li {{ margin: 5px 0; font-family: monospace; font-size: 16px; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; margin: 15px 0; border-radius: 5px; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{_serviceName}</h1>
            <h2>🔐 MFA 備用代碼</h2>
        </div>
        <div class=""content"">
            <p>親愛的 {userName}，</p>
            <p>以下是您的多因子認證備用代碼。當您無法使用主要 MFA 方法時，可以使用這些代碼登入。</p>
            <div class=""codes"">
                <p><strong>備用代碼：</strong></p>
                <ul>{codesList}</ul>
            </div>
            <div class=""warning"">
                <p><strong>⚠️ 重要提醒：</strong></p>
                <ul>
                    <li>每個備用代碼只能使用一次</li>
                    <li>請將這些代碼儲存在安全的地方</li>
                    <li>不要將代碼分享給他人</li>
                    <li>建議列印並安全保存</li>
                    <li>當備用代碼用完時，請重新產生新的代碼</li>
                </ul>
            </div>
        </div>
        <div class=""footer"">
            <p>{_serviceName} 安全團隊</p>
            <p>如有疑問，請聯繫：{_supportEmail}</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateBackupCodesText(string userName, List<string> backupCodes)
    {
        var codesList = string.Join("\n", backupCodes.Select(code => $"- {code}"));
        
        return $@"
{_serviceName} - MFA 備用代碼

親愛的 {userName}，

以下是您的多因子認證備用代碼。當您無法使用主要 MFA 方法時，可以使用這些代碼登入。

備用代碼：
{codesList}

⚠️ 重要提醒：
- 每個備用代碼只能使用一次
- 請將這些代碼儲存在安全的地方
- 不要將代碼分享給他人
- 建議列印並安全保存
- 當備用代碼用完時，請重新產生新的代碼

{_serviceName} 安全團隊
如有疑問，請聯繫：{_supportEmail}";
    }
}