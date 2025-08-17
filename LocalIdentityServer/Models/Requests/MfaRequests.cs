using System.ComponentModel.DataAnnotations;

namespace LocalIdentityServer.Models.Requests;

/// <summary>
/// TOTP 設定請求
/// </summary>
public class TotpSetupRequest
{
    /// <summary>
    /// 帳戶名稱 (用於 QR Code 顯示)
    /// </summary>
    [MaxLength(100)]
    public string? AccountName { get; set; }

    /// <summary>
    /// 設備名稱
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DeviceName { get; set; } = default!;
}

/// <summary>
/// TOTP 確認請求
/// </summary>
public class TotpConfirmRequest
{
    /// <summary>
    /// 設定期間的臨時密鑰
    /// </summary>
    [Required]
    public string Secret { get; set; } = default!;

    /// <summary>
    /// 設定權杖
    /// </summary>
    [Required]
    public string SetupToken { get; set; } = default!;

    /// <summary>
    /// 驗證代碼
    /// </summary>
    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "驗證代碼必須為6位數字")]
    public string Code { get; set; } = default!;

    /// <summary>
    /// 設備名稱
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DeviceName { get; set; } = default!;
}

/// <summary>
/// SMS 設定請求
/// </summary>
public class SmsSetupRequest
{
    /// <summary>
    /// 手機號碼 (國際格式)
    /// </summary>
    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = default!;

    /// <summary>
    /// 設備名稱
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DeviceName { get; set; } = default!;
}

/// <summary>
/// Email 設定請求
/// </summary>
public class EmailSetupRequest
{
    /// <summary>
    /// Email 地址
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string EmailAddress { get; set; } = default!;

    /// <summary>
    /// 設備名稱
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DeviceName { get; set; } = default!;
}

/// <summary>
/// MFA 驗證請求
/// </summary>
public class MfaVerifyRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// MFA 方法 (TOTP, SMS, EMAIL, BACKUPCODE)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Method { get; set; } = default!;

    /// <summary>
    /// 驗證代碼
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = default!;

    /// <summary>
    /// 客戶端ID (OAuth2)
    /// </summary>
    [MaxLength(100)]
    public string? ClientId { get; set; }

    /// <summary>
    /// 會話ID
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }

    /// <summary>
    /// 設備指紋
    /// </summary>
    [MaxLength(500)]
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// 地理位置資訊
    /// </summary>
    [MaxLength(500)]
    public string? GeoLocation { get; set; }
}

/// <summary>
/// MFA 挑戰請求
/// </summary>
public class MfaChallengeRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// MFA 方法 (SMS, EMAIL)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Method { get; set; } = default!;

    /// <summary>
    /// 客戶端ID (OAuth2)
    /// </summary>
    [MaxLength(100)]
    public string? ClientId { get; set; }

    /// <summary>
    /// 會話ID
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }
}

/// <summary>
/// MFA 重設請求 (管理員用)
/// </summary>
public class MfaResetRequest
{
    /// <summary>
    /// 目標使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 重設原因
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = default!;
}

/// <summary>
/// MFA 方法解鎖請求 (管理員用)
/// </summary>
public class MfaUnlockRequest
{
    /// <summary>
    /// 目標使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// MFA 方法
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Method { get; set; } = default!;
}

/// <summary>
/// 設置 TOTP 請求
/// </summary>
public class SetupTotpRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;
}

/// <summary>
/// 驗證 TOTP 請求
/// </summary>
public class VerifyTotpRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// TOTP 代碼
    /// </summary>
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = default!;
}

/// <summary>
/// 產生備援代碼請求
/// </summary>
public class GenerateBackupCodesRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;
}

/// <summary>
/// 驗證備援代碼請求
/// </summary>
public class VerifyBackupCodeRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 備援代碼
    /// </summary>
    [Required]
    public string Code { get; set; } = default!;
}

/// <summary>
/// 設置 SMS 請求
/// </summary>
public class SetupSmsRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 手機號碼
    /// </summary>
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = default!;
}

/// <summary>
/// 停用 MFA 請求
/// </summary>
public class DisableMfaRequest
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// MFA 方法
    /// </summary>
    [Required]
    public string Method { get; set; } = default!;
}