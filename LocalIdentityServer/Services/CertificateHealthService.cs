using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;

namespace LocalIdentityServer.Services;

/// <summary>
/// SSL 憑證健康檢查服務實作 - 遵循 SOLID 原則
/// 提供完整的憑證監控、驗證與健康狀態檢查功能
/// </summary>
public class CertificateHealthService : ICertificateHealthService
{
    private readonly ILogger<CertificateHealthService> _logger;
    private readonly ConcurrentDictionary<string, string> _monitoredCertificates;
    private readonly int _expiryWarningDays;

    public CertificateHealthService(
        ILogger<CertificateHealthService> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _monitoredCertificates = new ConcurrentDictionary<string, string>();
        _expiryWarningDays = configuration.GetValue<int>("SecurityHeaders:CertificateHealthCheck:ExpiryWarningDays", 30);
    }

    public async Task<CertificateHealthResult> CheckCertificateHealthAsync(string certificatePath)
    {
        try
        {
            if (!File.Exists(certificatePath))
            {
                return new CertificateHealthResult
                {
                    IsHealthy = false,
                    Errors = { $"Certificate file not found: {certificatePath}" }
                };
            }

            using var certificate = new X509Certificate2(certificatePath);
            return await CheckCertificateHealthAsync(certificate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check certificate health for {CertificatePath}", certificatePath);
            return new CertificateHealthResult
            {
                IsHealthy = false,
                Errors = { $"Failed to load certificate: {ex.Message}" }
            };
        }
    }

    public async Task<CertificateHealthResult> CheckCertificateHealthAsync(X509Certificate2 certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        var result = new CertificateHealthResult();

        try
        {
            // 基本憑證資訊
            result.SubjectName = certificate.SubjectName.Name;
            result.IssuerName = certificate.IssuerName.Name;
            result.SerialNumber = certificate.SerialNumber;
            result.Thumbprint = certificate.Thumbprint;
            result.NotBefore = certificate.NotBefore;
            result.NotAfter = certificate.NotAfter;
            result.SignatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName ?? "Unknown";

            // 計算到期天數
            result.DaysUntilExpiry = GetDaysUntilExpiry(certificate);
            result.IsExpired = result.DaysUntilExpiry < 0;
            result.IsExpiringSoon = result.DaysUntilExpiry <= _expiryWarningDays && result.DaysUntilExpiry >= 0;

            // 取得金鑰長度
            result.KeySize = GetKeySize(certificate);

            // 取得憑證用途
            result.KeyUsages = GetKeyUsages(certificate);
            result.ExtendedKeyUsages = GetExtendedKeyUsages(certificate);

            // 取得 SAN (Subject Alternative Names)
            result.SubjectAlternativeNames = GetSubjectAlternativeNames(certificate);

            // 檢查憑證有效性
            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore || now > certificate.NotAfter)
            {
                result.Errors.Add("Certificate is not currently valid (outside validity period)");
            }

            // 驗證憑證鏈
            result.IsChainValid = await ValidateCertificateChainAsync(certificate);
            if (!result.IsChainValid)
            {
                result.Errors.Add("Certificate chain validation failed");
            }

            // 檢查撤銷狀態
            result.IsRevoked = await IsCertificateRevokedAsync(certificate);
            if (result.IsRevoked)
            {
                result.Errors.Add("Certificate has been revoked");
            }

            // 檢查演算法強度
            CheckSignatureAlgorithmStrength(certificate, result);

            // 檢查金鑰長度
            CheckKeyStrength(result);

            // 新增到期警告
            if (result.IsExpiringSoon)
            {
                result.Warnings.Add($"Certificate expires in {result.DaysUntilExpiry} days");
            }

            if (result.IsExpired)
            {
                result.Errors.Add($"Certificate expired {Math.Abs(result.DaysUntilExpiry)} days ago");
            }

            // 設定整體健康狀態
            result.IsHealthy = !result.IsExpired && 
                             !result.IsRevoked && 
                             result.IsChainValid && 
                             result.Errors.Count == 0;

            _logger.LogDebug("Certificate health check completed for {Subject}: {Status}",
                result.SubjectName, result.IsHealthy ? "Healthy" : "Unhealthy");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during certificate health check for {Subject}", 
                certificate.SubjectName.Name);
            
            result.IsHealthy = false;
            result.Errors.Add($"Health check failed: {ex.Message}");
            return result;
        }
    }

    public int GetDaysUntilExpiry(X509Certificate2 certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        return (int)(certificate.NotAfter - DateTime.UtcNow).TotalDays;
    }

    public async Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate)
    {
        try
        {
            using var chain = new X509Chain();
            
            // 設定鏈驗證參數
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(30);

            var isValid = await Task.Run(() => chain.Build(certificate));

            if (!isValid)
            {
                _logger.LogWarning("Certificate chain validation failed for {Subject}. Errors: {Errors}",
                    certificate.SubjectName.Name,
                    string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation)));
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating certificate chain for {Subject}", 
                certificate.SubjectName.Name);
            return false;
        }
    }

    public async Task<bool> IsCertificateRevokedAsync(X509Certificate2 certificate)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10);

            var result = await Task.Run(() => chain.Build(certificate));
            
            // 檢查撤銷狀態
            var revokedStatus = chain.ChainStatus
                .Any(status => status.Status == X509ChainStatusFlags.Revoked);

            return revokedStatus;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check revocation status for {Subject}", 
                certificate.SubjectName.Name);
            return false; // 無法確認時假設未撤銷
        }
    }

    public async Task<List<CertificateHealthSummary>> GetAllCertificateHealthAsync()
    {
        var summaries = new List<CertificateHealthSummary>();

        foreach (var monitoredCert in _monitoredCertificates)
        {
            try
            {
                var healthResult = await CheckCertificateHealthAsync(monitoredCert.Key);
                
                var summary = new CertificateHealthSummary
                {
                    FriendlyName = monitoredCert.Value,
                    CertificatePath = monitoredCert.Key,
                    SubjectName = healthResult.SubjectName,
                    ExpiryDate = healthResult.NotAfter,
                    DaysUntilExpiry = healthResult.DaysUntilExpiry,
                    LastChecked = DateTime.UtcNow,
                    ErrorCount = healthResult.Errors.Count,
                    WarningCount = healthResult.Warnings.Count,
                    Status = DetermineHealthStatus(healthResult)
                };

                summaries.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health summary for {CertificatePath}", 
                    monitoredCert.Key);
                
                summaries.Add(new CertificateHealthSummary
                {
                    FriendlyName = monitoredCert.Value,
                    CertificatePath = monitoredCert.Key,
                    Status = CertificateHealthStatus.Unknown,
                    LastChecked = DateTime.UtcNow,
                    ErrorCount = 1
                });
            }
        }

        return summaries;
    }

    public void RegisterCertificateForMonitoring(string certificatePath, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(certificatePath))
            throw new ArgumentException("Certificate path cannot be null or empty", nameof(certificatePath));

        if (string.IsNullOrWhiteSpace(friendlyName))
            throw new ArgumentException("Friendly name cannot be null or empty", nameof(friendlyName));

        _monitoredCertificates.TryAdd(certificatePath, friendlyName);
        _logger.LogInformation("Registered certificate for monitoring: {FriendlyName} at {Path}",
            friendlyName, certificatePath);
    }

    public void UnregisterCertificateFromMonitoring(string certificatePath)
    {
        if (_monitoredCertificates.TryRemove(certificatePath, out var friendlyName))
        {
            _logger.LogInformation("Unregistered certificate from monitoring: {FriendlyName} at {Path}",
                friendlyName, certificatePath);
        }
    }

    #region Private Helper Methods

    private int GetKeySize(X509Certificate2 certificate)
    {
        try
        {
            return certificate.GetRSAPublicKey()?.KeySize ?? 
                   certificate.GetECDsaPublicKey()?.KeySize ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private List<string> GetKeyUsages(X509Certificate2 certificate)
    {
        var usages = new List<string>();
        
        try
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509KeyUsageExtension keyUsage)
                {
                    if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                        usages.Add("Digital Signature");
                    if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment))
                        usages.Add("Key Encipherment");
                    if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DataEncipherment))
                        usages.Add("Data Encipherment");
                    if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyAgreement))
                        usages.Add("Key Agreement");
                    if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign))
                        usages.Add("Certificate Signing");
                    if (keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.CrlSign))
                        usages.Add("CRL Signing");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse key usages for {Subject}", 
                certificate.SubjectName.Name);
        }

        return usages;
    }

    private List<string> GetExtendedKeyUsages(X509Certificate2 certificate)
    {
        var usages = new List<string>();
        
        try
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509EnhancedKeyUsageExtension enhancedKeyUsage)
                {
                    foreach (var oid in enhancedKeyUsage.EnhancedKeyUsages)
                    {
                        usages.Add(oid.FriendlyName ?? oid.Value ?? "Unknown");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse enhanced key usages for {Subject}", 
                certificate.SubjectName.Name);
        }

        return usages;
    }

    private List<string> GetSubjectAlternativeNames(X509Certificate2 certificate)
    {
        var sanList = new List<string>();
        
        try
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.17") // SAN extension OID
                {
                    // Note: .NET 框架對 SAN 的支援有限，這裡簡化處理
                    var rawData = extension.RawData;
                    // 實際實作中可能需要更複雜的 ASN.1 解析
                    // 此處省略複雜的 SAN 解析，在實際專案中可使用第三方程式庫
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse SAN for {Subject}", 
                certificate.SubjectName.Name);
        }

        return sanList;
    }

    private void CheckSignatureAlgorithmStrength(X509Certificate2 certificate, CertificateHealthResult result)
    {
        var algorithm = certificate.SignatureAlgorithm.FriendlyName?.ToLowerInvariant();
        
        if (algorithm?.Contains("md5") == true)
        {
            result.Errors.Add("Certificate uses weak MD5 signature algorithm");
        }
        else if (algorithm?.Contains("sha1") == true)
        {
            result.Warnings.Add("Certificate uses SHA-1 signature algorithm (deprecated)");
        }
    }

    private void CheckKeyStrength(CertificateHealthResult result)
    {
        if (result.KeySize > 0 && result.KeySize < 2048)
        {
            result.Errors.Add($"Certificate key size ({result.KeySize} bits) is below minimum recommended (2048 bits)");
        }
        else if (result.KeySize > 0 && result.KeySize < 4096)
        {
            result.Warnings.Add($"Certificate key size ({result.KeySize} bits) is below recommended (4096 bits)");
        }
    }

    private CertificateHealthStatus DetermineHealthStatus(CertificateHealthResult result)
    {
        if (result.Errors.Any())
            return CertificateHealthStatus.Error;
        
        if (result.Warnings.Any())
            return CertificateHealthStatus.Warning;
        
        if (result.IsHealthy)
            return CertificateHealthStatus.Healthy;
        
        return CertificateHealthStatus.Unknown;
    }

    #endregion
}