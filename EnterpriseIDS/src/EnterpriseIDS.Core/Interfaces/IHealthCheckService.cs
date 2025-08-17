using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 企業級健康檢查服務介面
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// 執行綜合健康檢查
    /// </summary>
    Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查系統就緒狀態
    /// </summary>
    Task<HealthCheckResponse> CheckReadinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查系統存活狀態
    /// </summary>
    Task<HealthCheckResponse> CheckLivenessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得健康檢查摘要
    /// </summary>
    Task<HealthCheckSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查特定組件健康狀態
    /// </summary>
    Task<HealthCheckResponse> CheckComponentHealthAsync(string componentName, CancellationToken cancellationToken = default);
}