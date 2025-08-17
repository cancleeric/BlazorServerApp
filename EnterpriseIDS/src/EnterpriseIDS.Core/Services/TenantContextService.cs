using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Core.Services;

/// <summary>
/// 租戶上下文值對象
/// </summary>
public class TenantContext
{
    /// <summary>
    /// 租戶 ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// 租戶標識符
    /// </summary>
    public string TenantSlug { get; set; } = string.Empty;

    /// <summary>
    /// 租戶名稱
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// 租戶類型
    /// </summary>
    public TenantType TenantType { get; set; }

    /// <summary>
    /// 租戶狀態
    /// </summary>
    public TenantStatus TenantStatus { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 父租戶 ID
    /// </summary>
    public Guid? ParentTenantId { get; set; }

    /// <summary>
    /// 主要網域
    /// </summary>
    public string? PrimaryDomain { get; set; }

    /// <summary>
    /// 允許的網域列表
    /// </summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// 啟用的功能列表
    /// </summary>
    public List<string> EnabledFeatures { get; set; } = new();

    /// <summary>
    /// 中繼資料
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 是否為超級管理員上下文
    /// </summary>
    public bool IsSuperAdmin { get; set; }

    /// <summary>
    /// 解析步驟（用於除錯）
    /// </summary>
    public List<string> ResolveSteps { get; set; } = new();

    /// <summary>
    /// 從 Tenant 實體建立上下文
    /// </summary>
    public static TenantContext FromTenant(Tenant tenant, bool isSuperAdmin = false)
    {
        return new TenantContext
        {
            TenantId = tenant.Id,
            TenantSlug = tenant.Slug,
            TenantName = tenant.Name,
            TenantType = tenant.TenantType,
            TenantStatus = tenant.Status,
            IsActive = tenant.IsActive(),
            ParentTenantId = tenant.ParentTenantId,
            PrimaryDomain = tenant.PrimaryDomain,
            AllowedDomains = tenant.AllowedDomains,
            EnabledFeatures = tenant.EnabledFeatures,
            Metadata = tenant.Metadata,
            IsSuperAdmin = isSuperAdmin
        };
    }

    /// <summary>
    /// 轉換為字典
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["tenantId"] = TenantId,
            ["tenantSlug"] = TenantSlug,
            ["tenantName"] = TenantName,
            ["tenantType"] = TenantType.ToString(),
            ["tenantStatus"] = TenantStatus.ToString(),
            ["isActive"] = IsActive,
            ["parentTenantId"] = ParentTenantId?.ToString() ?? string.Empty,
            ["primaryDomain"] = PrimaryDomain ?? string.Empty,
            ["allowedDomains"] = AllowedDomains,
            ["enabledFeatures"] = EnabledFeatures,
            ["metadata"] = Metadata,
            ["isSuperAdmin"] = IsSuperAdmin,
            ["resolveSteps"] = ResolveSteps
        };
    }
}

/// <summary>
/// 租戶上下文服務實作
/// </summary>
public class TenantContextService : ITenantContextService
{
    // 使用 AsyncLocal 來儲存當前租戶上下文，確保線程安全
    private static readonly AsyncLocal<TenantContext?> _currentTenant = new();
    private static readonly AsyncLocal<List<string>> _resolverStack = new();

    private readonly ITenantService _tenantService;

    public TenantContextService(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    /// <summary>
    /// 取得當前租戶 ID
    /// </summary>
    public Guid? GetCurrentTenantId()
    {
        return _currentTenant.Value?.TenantId;
    }

    /// <summary>
    /// 取得當前租戶上下文
    /// </summary>
    public TenantContext? GetCurrentTenantContext()
    {
        return _currentTenant.Value;
    }

    /// <summary>
    /// 取得當前租戶
    /// </summary>
    public async Task<Tenant?> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return null;

        return await _tenantService.GetByIdAsync(tenantId.Value, cancellationToken);
    }

    /// <summary>
    /// 設定當前租戶
    /// </summary>
    public void SetCurrentTenant(Guid? tenantId)
    {
        if (tenantId == null)
        {
            _currentTenant.Value = null;
            return;
        }

        // 如果只有 ID，創建基本的上下文，實際資料會在需要時載入
        if (_currentTenant.Value?.TenantId != tenantId)
        {
            _currentTenant.Value = new TenantContext
            {
                TenantId = tenantId.Value
            };
        }
    }

    /// <summary>
    /// 設定當前租戶上下文
    /// </summary>
    public void SetCurrentTenantContext(TenantContext? tenantContext)
    {
        _currentTenant.Value = tenantContext;
    }

    /// <summary>
    /// 檢查是否為超級管理員上下文
    /// </summary>
    public bool IsSuperAdminContext()
    {
        var context = _currentTenant.Value;
        if (context == null)
            return true; // 沒有租戶限制時視為超級管理員

        return context.IsSuperAdmin;
    }

    /// <summary>
    /// 檢查是否有租戶存取權限
    /// </summary>
    public bool HasTenantAccess(Guid tenantId)
    {
        // 超級管理員可以存取所有租戶
        if (IsSuperAdminContext())
            return true;

        var currentContext = _currentTenant.Value;
        if (currentContext == null)
            return false;

        // 當前租戶可以存取自己
        if (currentContext.TenantId == tenantId)
            return true;

        // 實作階層式租戶權限檢查
        // 檢查當前租戶是否為目標租戶的父級租戶
        if (IsParentTenant(currentContext.TenantId, tenantId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 檢查是否為父級租戶
    /// </summary>
    private bool IsParentTenant(Guid parentTenantId, Guid childTenantId)
    {
        // TODO: 實作階層式租戶權限檢查
        // 這需要查詢租戶的階層關係，暫時返回 false
        // 實際實作應該遞迴檢查租戶的 ParentTenantId 鏈
        return false;
    }

    /// <summary>
    /// 檢查功能是否啟用
    /// </summary>
    public async Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var context = _currentTenant.Value;
        if (context == null)
            return true; // 沒有租戶上下文時預設啟用

        // 如果上下文中沒有功能列表，從服務載入
        if (!context.EnabledFeatures.Any())
        {
            var tenant = await GetCurrentTenantAsync(cancellationToken);
            if (tenant != null)
            {
                context.EnabledFeatures = tenant.EnabledFeatures;
            }
        }

        return context.EnabledFeatures.Contains(featureName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 清除當前租戶上下文
    /// </summary>
    public void ClearCurrentTenant()
    {
        _currentTenant.Value = null;
        ClearResolverStack();
    }

    /// <summary>
    /// 添加解析步驟（用於除錯）
    /// </summary>
    public void AddResolverStep(string step)
    {
        var stack = _resolverStack.Value ??= new List<string>();
        stack.Add(step);
        _resolverStack.Value = stack;

        // 同時添加到當前上下文
        if (_currentTenant.Value != null)
        {
            _currentTenant.Value.ResolveSteps.Add(step);
        }
    }

    /// <summary>
    /// 取得解析步驟堆疊
    /// </summary>
    public List<string> GetResolverStack()
    {
        return _resolverStack.Value?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// 清除解析步驟堆疊
    /// </summary>
    public void ClearResolverStack()
    {
        _resolverStack.Value = new List<string>();
    }

    /// <summary>
    /// 建立租戶上下文管理器
    /// </summary>
    public TenantContextManager CreateContextManager(TenantContext? tenantContext)
    {
        return new TenantContextManager(this, tenantContext);
    }

    /// <summary>
    /// 建立租戶上下文管理器（根據租戶 ID）
    /// </summary>
    public TenantContextManager CreateContextManager(Guid? tenantId)
    {
        if (tenantId == null)
            return new TenantContextManager(this, null);

        var context = new TenantContext { TenantId = tenantId.Value };
        return new TenantContextManager(this, context);
    }

    /// <summary>
    /// 取得租戶上下文資訊（用於除錯）
    /// </summary>
    public Dictionary<string, object> GetContextInfo()
    {
        var context = _currentTenant.Value;
        if (context == null)
        {
            return new Dictionary<string, object>
            {
                ["hasTenantContext"] = false,
                ["isSuperAdmin"] = true,
                ["resolverStack"] = GetResolverStack()
            };
        }

        var info = context.ToDictionary();
        info["hasTenantContext"] = true;
        return info;
    }
}

/// <summary>
/// 租戶上下文管理器 - 用於臨時設定租戶上下文
/// </summary>
public class TenantContextManager : IDisposable
{
    private readonly TenantContextService _contextService;
    private readonly TenantContext? _previousContext;
    private readonly TenantContext? _newContext;
    private bool _disposed = false;

    public TenantContextManager(TenantContextService contextService, TenantContext? tenantContext)
    {
        _contextService = contextService;
        _previousContext = contextService.GetCurrentTenantContext();
        _newContext = tenantContext;

        // 設定新的上下文
        _contextService.SetCurrentTenantContext(_newContext);
    }

    /// <summary>
    /// 取得當前上下文
    /// </summary>
    public TenantContext? GetContext()
    {
        return _newContext;
    }

    /// <summary>
    /// 釋放資源，恢復之前的上下文
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _contextService.SetCurrentTenantContext(_previousContext);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 租戶篩選上下文管理器 - 用於臨時停用或啟用租戶篩選
/// </summary>
public class TenantFilterManager : IDisposable
{
    private readonly TenantContextService _contextService;
    private readonly bool _previousSuperAdminState;
    private readonly bool _newSuperAdminState;
    private bool _disposed = false;

    public TenantFilterManager(TenantContextService contextService, bool enableSuperAdmin)
    {
        _contextService = contextService;
        var currentContext = contextService.GetCurrentTenantContext();
        _previousSuperAdminState = currentContext?.IsSuperAdmin ?? true;
        _newSuperAdminState = enableSuperAdmin;

        // 修改當前上下文的超級管理員狀態
        if (currentContext != null)
        {
            currentContext.IsSuperAdmin = _newSuperAdminState;
        }
        else if (enableSuperAdmin)
        {
            // 如果沒有上下文但需要超級管理員權限，創建一個
            var superAdminContext = new TenantContext
            {
                TenantId = Guid.Empty,
                IsSuperAdmin = true,
                TenantSlug = "system",
                TenantName = "System"
            };
            contextService.SetCurrentTenantContext(superAdminContext);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            var currentContext = _contextService.GetCurrentTenantContext();
            if (currentContext != null)
            {
                currentContext.IsSuperAdmin = _previousSuperAdminState;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 擴展方法
/// </summary>
public static class TenantContextServiceExtensions
{
    /// <summary>
    /// 在沒有租戶篩選的情況下執行操作
    /// </summary>
    public static async Task<T> WithoutTenantFilterAsync<T>(this ITenantContextService contextService, Func<Task<T>> operation)
    {
        if (contextService is TenantContextService service)
        {
            using var manager = new TenantFilterManager(service, true);
            return await operation();
        }
        return await operation();
    }

    /// <summary>
    /// 在沒有租戶篩選的情況下執行操作
    /// </summary>
    public static async Task WithoutTenantFilterAsync(this ITenantContextService contextService, Func<Task> operation)
    {
        if (contextService is TenantContextService service)
        {
            using var manager = new TenantFilterManager(service, true);
            await operation();
        }
        else
        {
            await operation();
        }
    }

    /// <summary>
    /// 在指定租戶上下文中執行操作
    /// </summary>
    public static async Task<T> WithTenantContextAsync<T>(this ITenantContextService contextService, Guid tenantId, Func<Task<T>> operation)
    {
        if (contextService is TenantContextService service)
        {
            using var manager = service.CreateContextManager(tenantId);
            return await operation();
        }
        return await operation();
    }

    /// <summary>
    /// 在指定租戶上下文中執行操作
    /// </summary>
    public static async Task WithTenantContextAsync(this ITenantContextService contextService, Guid tenantId, Func<Task> operation)
    {
        if (contextService is TenantContextService service)
        {
            using var manager = service.CreateContextManager(tenantId);
            await operation();
        }
        else
        {
            await operation();
        }
    }
}