namespace EnterpriseIDS.Core.ValueObjects;

/// <summary>
/// 健康檢查回應模型
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// 健康狀態
    /// </summary>
    public HealthStatus Status { get; set; } = HealthStatus.Unhealthy;

    /// <summary>
    /// 檢查名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述訊息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 檢查時間
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 回應時間
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// 錯誤訊息 (如果有)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 額外的檢查資料
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// 服務版本
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>
/// 健康檢查摘要
/// </summary>
public class HealthCheckSummary
{
    /// <summary>
    /// 整體健康狀態
    /// </summary>
    public HealthStatus OverallStatus { get; set; } = HealthStatus.Unhealthy;

    /// <summary>
    /// 檢查時間
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 總檢查時間
    /// </summary>
    public TimeSpan TotalCheckTime { get; set; }

    /// <summary>
    /// 個別組件檢查結果
    /// </summary>
    public Dictionary<string, HealthCheckResponse> Components { get; set; } = new();

    /// <summary>
    /// 系統資訊
    /// </summary>
    public SystemInfo SystemInfo { get; set; } = new();

    /// <summary>
    /// 服務版本
    /// </summary>
    public string ServiceVersion { get; set; } = string.Empty;

    /// <summary>
    /// 建置資訊
    /// </summary>
    public string? BuildInfo { get; set; }
}

/// <summary>
/// 健康狀態列舉
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// 健康
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// 降級但可用
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// 系統資訊
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// 伺服器名稱
    /// </summary>
    public string ServerName { get; set; } = Environment.MachineName;

    /// <summary>
    /// 作業系統
    /// </summary>
    public string OperatingSystem { get; set; } = Environment.OSVersion.ToString();

    /// <summary>
    /// .NET 版本
    /// </summary>
    public string DotNetVersion { get; set; } = Environment.Version.ToString();

    /// <summary>
    /// 程序 ID
    /// </summary>
    public int ProcessId { get; set; } = Environment.ProcessId;

    /// <summary>
    /// 服務啟動時間
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 運行時間
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartTime;

    /// <summary>
    /// 使用的記憶體 (MB)
    /// </summary>
    public long WorkingSetMemoryMB { get; set; }

    /// <summary>
    /// GC 記憶體 (MB)
    /// </summary>
    public long GCMemoryMB { get; set; }

    /// <summary>
    /// CPU 使用率 (%)
    /// </summary>
    public double CpuUsagePercent { get; set; }
}

/// <summary>
/// 資料庫健康檢查結果
/// </summary>
public class DatabaseHealthCheck : HealthCheckResponse
{
    /// <summary>
    /// 連線字串名稱
    /// </summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// 資料庫類型
    /// </summary>
    public string DatabaseType { get; set; } = string.Empty;

    /// <summary>
    /// 連線字串 (脫敏)
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 查詢結果數量
    /// </summary>
    public int QueryResultCount { get; set; }
}

/// <summary>
/// Redis 健康檢查結果
/// </summary>
public class RedisHealthCheck : HealthCheckResponse
{
    /// <summary>
    /// Redis 伺服器地址
    /// </summary>
    public string ServerAddress { get; set; } = string.Empty;

    /// <summary>
    /// 資料庫索引
    /// </summary>
    public int DatabaseIndex { get; set; }

    /// <summary>
    /// 連線數量
    /// </summary>
    public int ConnectionCount { get; set; }

    /// <summary>
    /// 使用的記憶體
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// 操作次數
    /// </summary>
    public long OperationsPerSecond { get; set; }
}