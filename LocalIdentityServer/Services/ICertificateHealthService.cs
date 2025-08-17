using System.Security.Cryptography.X509Certificates;

namespace LocalIdentityServer.Services;

/// <summary>
/// SSL 憑證健康檢查服務介面 - 遵循介面隔離原則 (ISP)
/// 提供憑證監控、驗證與健康狀態檢查功能
/// </summary>
public interface ICertificateHealthService
{
    /// <summary>
    /// 檢查憑證健康狀態
    /// </summary>
    /// <param name="certificatePath">憑證檔案路徑</param>
    /// <returns>憑證健康狀態</returns>
    Task<CertificateHealthResult> CheckCertificateHealthAsync(string certificatePath);

    /// <summary>
    /// 檢查憑證健康狀態 (使用憑證物件)
    /// </summary>
    /// <param name="certificate">X509 憑證物件</param>
    /// <returns>憑證健康狀態</returns>
    Task<CertificateHealthResult> CheckCertificateHealthAsync(X509Certificate2 certificate);

    /// <summary>
    /// 取得憑證到期天數
    /// </summary>
    /// <param name="certificate">X509 憑證物件</param>
    /// <returns>到期天數</returns>
    int GetDaysUntilExpiry(X509Certificate2 certificate);

    /// <summary>
    /// 驗證憑證鏈
    /// </summary>
    /// <param name="certificate">X509 憑證物件</param>
    /// <returns>憑證鏈驗證結果</returns>
    Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate);

    /// <summary>
    /// 檢查憑證撤銷狀態
    /// </summary>
    /// <param name="certificate">X509 憑證物件</param>
    /// <returns>是否已撤銷</returns>
    Task<bool> IsCertificateRevokedAsync(X509Certificate2 certificate);

    /// <summary>
    /// 取得所有憑證健康狀態摘要
    /// </summary>
    /// <returns>所有憑證的健康狀態摘要</returns>
    Task<List<CertificateHealthSummary>> GetAllCertificateHealthAsync();

    /// <summary>
    /// 註冊憑證監控
    /// </summary>
    /// <param name="certificatePath">憑證檔案路徑</param>
    /// <param name="friendlyName">憑證友善名稱</param>
    void RegisterCertificateForMonitoring(string certificatePath, string friendlyName);

    /// <summary>
    /// 移除憑證監控
    /// </summary>
    /// <param name="certificatePath">憑證檔案路徑</param>
    void UnregisterCertificateFromMonitoring(string certificatePath);
}

/// <summary>
/// 憑證健康檢查結果
/// </summary>
public class CertificateHealthResult
{
    /// <summary>
    /// 憑證是否健康
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// 憑證主體名稱
    /// </summary>
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>
    /// 憑證發行者
    /// </summary>
    public string IssuerName { get; set; } = string.Empty;

    /// <summary>
    /// 憑證序號
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// 憑證指紋 (SHA-1)
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// 憑證生效日期
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// 憑證到期日期
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// 到期天數
    /// </summary>
    public int DaysUntilExpiry { get; set; }

    /// <summary>
    /// 是否即將到期 (預設 30 天內)
    /// </summary>
    public bool IsExpiringSoon { get; set; }

    /// <summary>
    /// 是否已到期
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// 憑證鏈是否有效
    /// </summary>
    public bool IsChainValid { get; set; }

    /// <summary>
    /// 是否已撤銷
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// 健康檢查錯誤訊息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 健康檢查警告訊息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 憑證用途 (Key Usage)
    /// </summary>
    public List<string> KeyUsages { get; set; } = new();

    /// <summary>
    /// 憑證擴充用途 (Extended Key Usage)
    /// </summary>
    public List<string> ExtendedKeyUsages { get; set; } = new();

    /// <summary>
    /// 主體別名 (Subject Alternative Names)
    /// </summary>
    public List<string> SubjectAlternativeNames { get; set; } = new();

    /// <summary>
    /// 憑證演算法
    /// </summary>
    public string SignatureAlgorithm { get; set; } = string.Empty;

    /// <summary>
    /// 金鑰長度
    /// </summary>
    public int KeySize { get; set; }

    /// <summary>
    /// 檢查時間
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 憑證健康狀態摘要
/// </summary>
public class CertificateHealthSummary
{
    /// <summary>
    /// 憑證友善名稱
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// 憑證檔案路徑
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// 憑證主體名稱
    /// </summary>
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>
    /// 憑證到期日期
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// 到期天數
    /// </summary>
    public int DaysUntilExpiry { get; set; }

    /// <summary>
    /// 健康狀態
    /// </summary>
    public CertificateHealthStatus Status { get; set; }

    /// <summary>
    /// 最後檢查時間
    /// </summary>
    public DateTime LastChecked { get; set; }

    /// <summary>
    /// 錯誤數量
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// 警告數量
    /// </summary>
    public int WarningCount { get; set; }
}

/// <summary>
/// 憑證健康狀態列舉
/// </summary>
public enum CertificateHealthStatus
{
    /// <summary>
    /// 健康
    /// </summary>
    Healthy,

    /// <summary>
    /// 警告 (即將到期)
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤 (已到期或無效)
    /// </summary>
    Error,

    /// <summary>
    /// 未知 (檢查失敗)
    /// </summary>
    Unknown
}