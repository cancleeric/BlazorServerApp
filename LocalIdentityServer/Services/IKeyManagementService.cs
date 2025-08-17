using Microsoft.IdentityModel.Tokens;
using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Services;

/// <summary>
/// 企業級金鑰管理服務介面 - 遵循介面隔離原則 (ISP)
/// 提供統一的金鑰生命週期管理功能
/// </summary>
public interface IKeyManagementService
{
    /// <summary>
    /// 取得當前主要簽章金鑰
    /// </summary>
    /// <returns>主要簽章金鑰，如果不存在則為 null</returns>
    Task<SecurityKey?> GetCurrentSigningKeyAsync();

    /// <summary>
    /// 取得所有有效的驗證金鑰 (用於 JWKS 端點)
    /// </summary>
    /// <returns>有效的驗證金鑰集合</returns>
    Task<IEnumerable<SecurityKey>> GetValidationKeysAsync();

    /// <summary>
    /// 生成新的 RSA 金鑰
    /// </summary>
    /// <param name="keySize">金鑰大小 (2048 或 4096)</param>
    /// <param name="algorithm">簽章演算法 (預設 RS256)</param>
    /// <param name="validityPeriod">有效期間 (預設 1 年)</param>
    /// <returns>新生成的金鑰 ID</returns>
    Task<string> GenerateNewKeyAsync(int keySize = 2048, string algorithm = "RS256", TimeSpan? validityPeriod = null);

    /// <summary>
    /// 設定主要簽章金鑰
    /// </summary>
    /// <param name="keyId">金鑰 ID</param>
    Task SetPrimaryKeyAsync(string keyId);

    /// <summary>
    /// 撤銷金鑰
    /// </summary>
    /// <param name="keyId">要撤銷的金鑰 ID</param>
    /// <param name="reason">撤銷原因</param>
    Task RevokeKeyAsync(string keyId, string? reason = null);

    /// <summary>
    /// 執行金鑰輪替檢查並在必要時輪替
    /// </summary>
    /// <returns>是否執行了輪替</returns>
    Task<bool> RotateKeyIfNeededAsync();

    /// <summary>
    /// 強制執行金鑰輪替
    /// </summary>
    /// <returns>新金鑰的 ID</returns>
    Task<string> ForceKeyRotationAsync();

    /// <summary>
    /// 清理過期的金鑰
    /// </summary>
    /// <returns>被清理的金鑰數量</returns>
    Task<int> CleanupExpiredKeysAsync();

    /// <summary>
    /// 匯出公鑰為 JWK 格式 (用於 JWKS)
    /// </summary>
    /// <param name="keyId">金鑰 ID</param>
    /// <returns>JWK 格式的公鑰資料</returns>
    Task<string?> ExportPublicKeyAsJwkAsync(string keyId);

    /// <summary>
    /// 取得金鑰統計資訊
    /// </summary>
    /// <returns>金鑰統計資訊</returns>
    Task<KeyStatistics> GetKeyStatisticsAsync();

    /// <summary>
    /// 檢查金鑰健康狀態
    /// </summary>
    /// <returns>健康檢查結果</returns>
    Task<KeyHealthCheckResult> CheckKeyHealthAsync();
}

/// <summary>
/// 金鑰統計資訊
/// </summary>
public record KeyStatistics
{
    /// <summary>
    /// 總金鑰數量
    /// </summary>
    public int TotalKeys { get; init; }

    /// <summary>
    /// 活躍金鑰數量
    /// </summary>
    public int ActiveKeys { get; init; }

    /// <summary>
    /// 過期金鑰數量
    /// </summary>
    public int ExpiredKeys { get; init; }

    /// <summary>
    /// 撤銷金鑰數量
    /// </summary>
    public int RevokedKeys { get; init; }

    /// <summary>
    /// 下次輪替時間
    /// </summary>
    public DateTime? NextRotationTime { get; init; }

    /// <summary>
    /// 主要金鑰資訊
    /// </summary>
    public KeyInfo? PrimaryKey { get; init; }
}

/// <summary>
/// 金鑰資訊
/// </summary>
public record KeyInfo
{
    /// <summary>
    /// 金鑰 ID
    /// </summary>
    public string KeyId { get; init; } = default!;

    /// <summary>
    /// 演算法
    /// </summary>
    public string Algorithm { get; init; } = default!;

    /// <summary>
    /// 金鑰大小
    /// </summary>
    public int KeySize { get; init; }

    /// <summary>
    /// 創建時間
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 到期時間
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// 是否為主要金鑰
    /// </summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// 金鑰健康檢查結果
/// </summary>
public record KeyHealthCheckResult
{
    /// <summary>
    /// 整體健康狀態
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// 檢查結果詳細資訊
    /// </summary>
    public List<string> Issues { get; init; } = new();

    /// <summary>
    /// 警告資訊
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// 檢查時間
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}