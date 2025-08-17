namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 租戶類型枚舉
/// </summary>
public enum TenantType
{
    /// <summary>
    /// 小型企業
    /// </summary>
    Small = 1,
    
    /// <summary>
    /// 中型企業
    /// </summary>
    Medium = 2,
    
    /// <summary>
    /// 大型企業
    /// </summary>
    Enterprise = 3,
    
    /// <summary>
    /// 政府機構
    /// </summary>
    Government = 4,
    
    /// <summary>
    /// 教育機構
    /// </summary>
    Education = 5,
    
    /// <summary>
    /// 非營利組織
    /// </summary>
    NonProfit = 6
}

/// <summary>
/// 租戶狀態枚舉
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// 待核准
    /// </summary>
    Pending = 1,
    
    /// <summary>
    /// 啟用中
    /// </summary>
    Active = 2,
    
    /// <summary>
    /// 暫停
    /// </summary>
    Suspended = 3,
    
    /// <summary>
    /// 已停用
    /// </summary>
    Disabled = 4,
    
    /// <summary>
    /// 已刪除
    /// </summary>
    Deleted = 5
}

/// <summary>
/// 租戶訂閱計劃枚舉
/// </summary>
public enum SubscriptionPlan
{
    /// <summary>
    /// 免費計劃
    /// </summary>
    Free = 1,
    
    /// <summary>
    /// 基本計劃
    /// </summary>
    Basic = 2,
    
    /// <summary>
    /// 專業計劃
    /// </summary>
    Professional = 3,
    
    /// <summary>
    /// 企業計劃
    /// </summary>
    Enterprise = 4,
    
    /// <summary>
    /// 自訂計劃
    /// </summary>
    Custom = 5
}