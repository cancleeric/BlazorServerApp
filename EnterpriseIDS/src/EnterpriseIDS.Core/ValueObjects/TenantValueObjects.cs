namespace EnterpriseIDS.Core.ValueObjects;

/// <summary>
/// 租戶品牌設定值對象
/// </summary>
public class TenantBranding
{
    /// <summary>
    /// 主要顏色
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// 次要顏色
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Logo URL
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// 小圖示 URL
    /// </summary>
    public string? FaviconUrl { get; set; }

    /// <summary>
    /// 自訂 CSS
    /// </summary>
    public string? CustomCss { get; set; }

    /// <summary>
    /// 公司名稱顯示
    /// </summary>
    public string? CompanyDisplayName { get; set; }

    /// <summary>
    /// 頁首文字
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// 頁尾文字
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    /// 登入頁面背景圖片 URL
    /// </summary>
    public string? LoginBackgroundUrl { get; set; }

    /// <summary>
    /// 自訂主題名稱
    /// </summary>
    public string? Theme { get; set; } = "default";
}

/// <summary>
/// 租戶配額設定值對象
/// </summary>
public class TenantQuotas
{
    /// <summary>
    /// 最大用戶數量
    /// </summary>
    public int MaxUsers { get; set; } = 10;

    /// <summary>
    /// 最大客戶端應用程式數量
    /// </summary>
    public int MaxClients { get; set; } = 5;

    /// <summary>
    /// 最大 API 資源數量
    /// </summary>
    public int MaxApiResources { get; set; } = 10;

    /// <summary>
    /// 最大身份資源數量
    /// </summary>
    public int MaxIdentityResources { get; set; } = 10;

    /// <summary>
    /// 最大角色數量
    /// </summary>
    public int MaxRoles { get; set; } = 20;

    /// <summary>
    /// 最大群組數量
    /// </summary>
    public int MaxGroups { get; set; } = 50;

    /// <summary>
    /// 每日 API 呼叫限制
    /// </summary>
    public int DailyApiCallLimit { get; set; } = 10000;

    /// <summary>
    /// 每小時 API 呼叫限制
    /// </summary>
    public int HourlyApiCallLimit { get; set; } = 1000;

    /// <summary>
    /// 資料儲存限制（MB）
    /// </summary>
    public long StorageLimitMB { get; set; } = 1000;

    /// <summary>
    /// 檔案上傳限制（MB）
    /// </summary>
    public int FileUploadLimitMB { get; set; } = 10;

    /// <summary>
    /// 最大並發連線數
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 100;

    /// <summary>
    /// 備份保留天數
    /// </summary>
    public int BackupRetentionDays { get; set; } = 30;
}

/// <summary>
/// 租戶安全設定值對象
/// </summary>
public class TenantSecuritySettings
{
    /// <summary>
    /// 強制 HTTPS
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// 強制多因子認證
    /// </summary>
    public bool RequireMfa { get; set; } = false;

    /// <summary>
    /// 密碼最小長度
    /// </summary>
    public int PasswordMinLength { get; set; } = 8;

    /// <summary>
    /// 密碼需要數字
    /// </summary>
    public bool PasswordRequireDigit { get; set; } = true;

    /// <summary>
    /// 密碼需要小寫字母
    /// </summary>
    public bool PasswordRequireLowercase { get; set; } = true;

    /// <summary>
    /// 密碼需要大寫字母
    /// </summary>
    public bool PasswordRequireUppercase { get; set; } = true;

    /// <summary>
    /// 密碼需要特殊字元
    /// </summary>
    public bool PasswordRequireNonAlphanumeric { get; set; } = true;

    /// <summary>
    /// 密碼過期天數
    /// </summary>
    public int PasswordExpiryDays { get; set; } = 90;

    /// <summary>
    /// 密碼歷史記錄數量
    /// </summary>
    public int PasswordHistoryCount { get; set; } = 5;

    /// <summary>
    /// 帳戶鎖定失敗次數
    /// </summary>
    public int AccountLockoutFailedAttempts { get; set; } = 5;

    /// <summary>
    /// 帳戶鎖定時間（分鐘）
    /// </summary>
    public int AccountLockoutTimeSpanMinutes { get; set; } = 30;

    /// <summary>
    /// Session 逾時時間（分鐘）
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// 允許的 IP 範圍列表
    /// </summary>
    public List<string> AllowedIpRanges { get; set; } = new();

    /// <summary>
    /// 封鎖的 IP 範圍列表
    /// </summary>
    public List<string> BlockedIpRanges { get; set; } = new();

    /// <summary>
    /// 允許來自未知裝置的登入
    /// </summary>
    public bool AllowUnknownDevices { get; set; } = true;

    /// <summary>
    /// 需要裝置確認
    /// </summary>
    public bool RequireDeviceConfirmation { get; set; } = false;

    /// <summary>
    /// JWT Token 有效期（分鐘）
    /// </summary>
    public int JwtTokenExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh Token 有效期（天）
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 30;

    /// <summary>
    /// 啟用 Refresh Token 輪替
    /// </summary>
    public bool EnableRefreshTokenRotation { get; set; } = true;

    /// <summary>
    /// 稽核日誌等級
    /// </summary>
    public string AuditLogLevel { get; set; } = "Information";

    /// <summary>
    /// 啟用登入通知
    /// </summary>
    public bool EnableLoginNotifications { get; set; } = false;

    /// <summary>
    /// 啟用可疑活動偵測
    /// </summary>
    public bool EnableSuspiciousActivityDetection { get; set; } = true;
}