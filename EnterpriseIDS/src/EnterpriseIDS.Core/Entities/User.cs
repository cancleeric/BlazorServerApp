using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 使用者實體
/// </summary>
public class User : TenantAwareEntity
{
    /// <summary>
    /// 使用者名稱 (唯一識別)
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件地址
    /// </summary>
    [Required]
    [StringLength(320)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 名字
    /// </summary>
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// 姓氏
    /// </summary>
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱
    /// </summary>
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 部門
    /// </summary>
    [StringLength(200)]
    public string? Department { get; set; }

    /// <summary>
    /// 職務
    /// </summary>
    [StringLength(200)]
    public string? JobTitle { get; set; }

    /// <summary>
    /// 電話號碼
    /// </summary>
    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// 行動電話
    /// </summary>
    [StringLength(50)]
    public string? MobileNumber { get; set; }

    /// <summary>
    /// 辦公室位置
    /// </summary>
    [StringLength(200)]
    public string? Office { get; set; }

    /// <summary>
    /// 主管使用者 ID
    /// </summary>
    public Guid? ManagerId { get; set; }

    /// <summary>
    /// 主管使用者
    /// </summary>
    public User? Manager { get; set; }

    /// <summary>
    /// 直屬下屬
    /// </summary>
    public ICollection<User> DirectReports { get; set; } = new List<User>();

    /// <summary>
    /// 使用者狀態
    /// </summary>
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>
    /// 最後登入時間
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 密碼最後變更時間
    /// </summary>
    public DateTime? PasswordLastChangedAt { get; set; }

    /// <summary>
    /// 密碼過期時間
    /// </summary>
    public DateTime? PasswordExpiresAt { get; set; }

    /// <summary>
    /// 帳號鎖定時間
    /// </summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>
    /// 帳號鎖定到期時間
    /// </summary>
    public DateTime? LockoutEndAt { get; set; }

    /// <summary>
    /// 失敗登入次數
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// 是否啟用多因子認證
    /// </summary>
    public bool IsMfaEnabled { get; set; } = false;

    /// <summary>
    /// 偏好語言
    /// </summary>
    [StringLength(10)]
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// 時區
    /// </summary>
    [StringLength(100)]
    public string? TimeZone { get; set; }

    // LDAP 相關屬性

    /// <summary>
    /// LDAP 辨別名稱 (Distinguished Name)
    /// </summary>
    [StringLength(500)]
    public string? LdapDistinguishedName { get; set; }

    /// <summary>
    /// LDAP 物件 GUID
    /// </summary>
    public string? LdapObjectGuid { get; set; }

    /// <summary>
    /// LDAP 安全識別碼 (SID)
    /// </summary>
    [StringLength(200)]
    public string? LdapSecurityIdentifier { get; set; }

    /// <summary>
    /// 是否來自 LDAP
    /// </summary>
    public bool IsFromLdap { get; set; } = false;

    /// <summary>
    /// 最後 LDAP 同步時間
    /// </summary>
    public DateTime? LastLdapSyncAt { get; set; }

    /// <summary>
    /// LDAP 同步雜湊 (用於偵測變更)
    /// </summary>
    [StringLength(64)]
    public string? LdapSyncHash { get; set; }

    // 導航屬性

    /// <summary>
    /// 使用者群組成員資格
    /// </summary>
    public ICollection<UserGroupMembership> GroupMemberships { get; set; } = new List<UserGroupMembership>();

    /// <summary>
    /// 使用者角色
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    // 業務邏輯方法

    /// <summary>
    /// 取得完整名稱
    /// </summary>
    public string GetFullName()
    {
        if (!string.IsNullOrEmpty(DisplayName))
            return DisplayName;
        
        var fullName = $"{FirstName} {LastName}".Trim();
        return string.IsNullOrEmpty(fullName) ? Username : fullName;
    }

    /// <summary>
    /// 檢查是否為活躍使用者
    /// </summary>
    public bool IsActive()
    {
        return Status == UserStatus.Active && !IsDeleted;
    }

    /// <summary>
    /// 檢查是否被鎖定
    /// </summary>
    public bool IsLocked()
    {
        return Status == UserStatus.Locked || 
               (LockoutEndAt.HasValue && LockoutEndAt.Value > DateTime.UtcNow);
    }

    /// <summary>
    /// 檢查密碼是否過期
    /// </summary>
    public bool IsPasswordExpired()
    {
        return Status == UserStatus.PasswordExpired ||
               (PasswordExpiresAt.HasValue && PasswordExpiresAt.Value <= DateTime.UtcNow);
    }

    /// <summary>
    /// 重設失敗登入次數
    /// </summary>
    public void ResetFailedLoginAttempts()
    {
        FailedLoginAttempts = 0;
        if (Status == UserStatus.Locked)
        {
            Status = UserStatus.Active;
        }
        LockedAt = null;
        LockoutEndAt = null;
    }

    /// <summary>
    /// 增加失敗登入次數
    /// </summary>
    public void IncrementFailedLoginAttempts(int maxAttempts = 5, int lockoutMinutes = 30)
    {
        FailedLoginAttempts++;
        
        if (FailedLoginAttempts >= maxAttempts)
        {
            Status = UserStatus.Locked;
            LockedAt = DateTime.UtcNow;
            LockoutEndAt = DateTime.UtcNow.AddMinutes(lockoutMinutes);
        }
    }

    /// <summary>
    /// 更新最後登入時間
    /// </summary>
    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        ResetFailedLoginAttempts();
    }

    /// <summary>
    /// 更新 LDAP 同步資訊
    /// </summary>
    public void UpdateLdapSync(string syncHash)
    {
        LastLdapSyncAt = DateTime.UtcNow;
        LdapSyncHash = syncHash;
    }

    /// <summary>
    /// 檢查是否需要 LDAP 同步
    /// </summary>
    public bool RequiresLdapSync(string currentHash)
    {
        return !IsFromLdap || 
               string.IsNullOrEmpty(LdapSyncHash) || 
               LdapSyncHash != currentHash ||
               !LastLdapSyncAt.HasValue ||
               LastLdapSyncAt.Value < DateTime.UtcNow.AddHours(-1); // 超過1小時強制同步
    }
}