using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Core.Services;

/// <summary>
/// 租戶解析服務實作
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly ITenantRepository _tenantRepository;
    private readonly TenantContextService _contextService;

    public TenantResolver(ITenantRepository tenantRepository, TenantContextService contextService)
    {
        _tenantRepository = tenantRepository;
        _contextService = contextService;
    }

    /// <summary>
    /// 從 HTTP 請求解析租戶
    /// </summary>
    public async Task<Tenant?> ResolveAsync(string? host = null, string? path = null, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        _contextService.ClearResolverStack();

        // 1. 優先從 HTTP 標頭解析
        if (headers != null)
        {
            var tenant = await ResolveFromHeaderAsync(headers, cancellationToken);
            if (tenant != null)
                return tenant;
        }

        // 2. 從 URL 路徑解析
        if (!string.IsNullOrEmpty(path))
        {
            var tenant = await ResolveFromPathAsync(path, cancellationToken);
            if (tenant != null)
                return tenant;
        }

        // 3. 從子網域解析
        if (!string.IsNullOrEmpty(host))
        {
            var tenant = await ResolveFromSubdomainAsync(host, cancellationToken);
            if (tenant != null)
                return tenant;
        }

        // 4. 使用預設租戶
        var defaultTenant = await GetDefaultTenantAsync(cancellationToken);
        if (defaultTenant != null)
            return defaultTenant;

        _contextService.AddResolverStep("no_tenant_resolved");
        return null;
    }

    /// <summary>
    /// 從子網域解析租戶
    /// </summary>
    public async Task<Tenant?> ResolveFromSubdomainAsync(string host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(host))
            return null;

        _contextService.AddResolverStep($"subdomain:{host}");

        // 提取子網域部分
        var parts = host.Split('.');
        if (parts.Length < 2)
        {
            _contextService.AddResolverStep("subdomain:invalid_format");
            return null;
        }

        var subdomain = parts[0].ToLowerInvariant();

        // 排除常見的非租戶子網域
        var systemSubdomains = new[] { "www", "api", "admin", "app", "dashboard", "static", "cdn", "mail", "ftp" };
        if (systemSubdomains.Contains(subdomain))
        {
            _contextService.AddResolverStep($"subdomain:system_subdomain:{subdomain}");
            return null;
        }

        try
        {
            // 嘗試根據子網域作為 slug 查詢租戶
            var tenant = await _tenantRepository.GetBySlugAsync(subdomain, cancellationToken);
            if (tenant != null && tenant.Status == TenantStatus.Active)
            {
                _contextService.AddResolverStep($"subdomain:found_by_slug:{subdomain}");
                return tenant;
            }

            // 嘗試根據完整網域查詢租戶
            tenant = await _tenantRepository.GetByDomainAsync(host, cancellationToken);
            if (tenant != null && tenant.Status == TenantStatus.Active)
            {
                _contextService.AddResolverStep($"subdomain:found_by_domain:{host}");
                return tenant;
            }

            _contextService.AddResolverStep($"subdomain:not_found:{subdomain}");
        }
        catch (Exception ex)
        {
            _contextService.AddResolverStep($"subdomain:error:{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 從路徑解析租戶
    /// </summary>
    public async Task<Tenant?> ResolveFromPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        _contextService.AddResolverStep($"path:{path}");

        // 支援的路徑格式：
        // /t/tenant-slug/...
        // /tenant/tenant-slug/...
        // /organization/tenant-slug/...

        var pathPrefixes = new[] { "/t/", "/tenant/", "/organization/", "/org/" };
        
        foreach (var prefix in pathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainingPath = path.Substring(prefix.Length);
                var parts = remainingPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length > 0)
                {
                    var tenantSlug = parts[0].ToLowerInvariant();
                    _contextService.AddResolverStep($"path:extracted_slug:{tenantSlug}");

                    try
                    {
                        var tenant = await _tenantRepository.GetBySlugAsync(tenantSlug, cancellationToken);
                        if (tenant != null && tenant.Status == TenantStatus.Active)
                        {
                            _contextService.AddResolverStep($"path:found:{tenantSlug}");
                            return tenant;
                        }
                        else
                        {
                            _contextService.AddResolverStep($"path:not_found_or_inactive:{tenantSlug}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _contextService.AddResolverStep($"path:error:{ex.Message}");
                    }
                }
            }
        }

        _contextService.AddResolverStep("path:no_matching_prefix");
        return null;
    }

    /// <summary>
    /// 從標頭解析租戶
    /// </summary>
    public async Task<Tenant?> ResolveFromHeaderAsync(IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (headers == null || !headers.Any())
            return null;

        _contextService.AddResolverStep("header:checking");

        // 檢查各種可能的標頭
        var headerNames = new[]
        {
            "X-Tenant-ID",
            "X-TenantId", 
            "TenantId",
            "X-Tenant-Slug",
            "X-TenantSlug",
            "TenantSlug",
            "X-Organization-ID",
            "X-OrganizationId",
            "OrganizationId"
        };

        foreach (var headerName in headerNames)
        {
            if (headers.TryGetValue(headerName, out var headerValue) && !string.IsNullOrEmpty(headerValue))
            {
                _contextService.AddResolverStep($"header:found:{headerName}={headerValue}");

                try
                {
                    // 嘗試作為 GUID 解析（租戶 ID）
                    if (Guid.TryParse(headerValue, out var tenantId))
                    {
                        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
                        if (tenant != null && tenant.Status == TenantStatus.Active)
                        {
                            _contextService.AddResolverStep($"header:found_by_id:{tenantId}");
                            return tenant;
                        }
                    }

                    // 嘗試作為 Slug 解析
                    var tenantBySlug = await _tenantRepository.GetBySlugAsync(headerValue.ToLowerInvariant(), cancellationToken);
                    if (tenantBySlug != null && tenantBySlug.Status == TenantStatus.Active)
                    {
                        _contextService.AddResolverStep($"header:found_by_slug:{headerValue}");
                        return tenantBySlug;
                    }

                    _contextService.AddResolverStep($"header:not_found:{headerName}={headerValue}");
                }
                catch (Exception ex)
                {
                    _contextService.AddResolverStep($"header:error:{headerName}:{ex.Message}");
                }
            }
        }

        _contextService.AddResolverStep("header:no_valid_headers");
        return null;
    }

    /// <summary>
    /// 取得預設租戶
    /// </summary>
    public async Task<Tenant?> GetDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        _contextService.AddResolverStep("default:checking");

        try
        {
            var defaultTenant = await _tenantRepository.GetDefaultTenantAsync(cancellationToken);
            if (defaultTenant != null && defaultTenant.Status == TenantStatus.Active)
            {
                _contextService.AddResolverStep($"default:found:{defaultTenant.Slug}");
                return defaultTenant;
            }

            _contextService.AddResolverStep("default:not_found");
        }
        catch (Exception ex)
        {
            _contextService.AddResolverStep($"default:error:{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 驗證租戶是否有效
    /// </summary>
    public Task<bool> ValidateTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (tenant == null)
            return Task.FromResult(false);

        // 檢查租戶狀態
        if (tenant.Status != TenantStatus.Active)
        {
            _contextService.AddResolverStep($"validation:inactive_status:{tenant.Status}");
            return Task.FromResult(false);
        }

        // 檢查軟刪除
        if (tenant.IsDeleted)
        {
            _contextService.AddResolverStep("validation:soft_deleted");
            return Task.FromResult(false);
        }

        // 檢查訂閱有效性
        if (!tenant.HasValidSubscription() && !tenant.IsInTrial())
        {
            _contextService.AddResolverStep("validation:invalid_subscription");
            return Task.FromResult(false);
        }

        // 檢查試用期（如果適用）
        if (tenant.TrialEndDate.HasValue && tenant.TrialEndDate.Value < DateTime.UtcNow)
        {
            if (!tenant.HasValidSubscription())
            {
                _contextService.AddResolverStep("validation:trial_expired");
                return Task.FromResult(false);
            }
        }

        _contextService.AddResolverStep("validation:passed");
        return Task.FromResult(true);
    }

    /// <summary>
    /// 解析並驗證租戶
    /// </summary>
    public async Task<Tenant?> ResolveAndValidateAsync(string? host = null, string? path = null, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveAsync(host, path, headers, cancellationToken);
        
        if (tenant != null)
        {
            var isValid = await ValidateTenantAsync(tenant, cancellationToken);
            if (!isValid)
                return null;
        }

        return tenant;
    }

    /// <summary>
    /// 取得解析器統計資訊
    /// </summary>
    public ResolverStatistics GetStatistics()
    {
        return new ResolverStatistics
        {
            ResolverSteps = _contextService.GetResolverStack(),
            ResolverCount = _contextService.GetResolverStack().Count,
            LastResolvedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// 解析器統計資訊
/// </summary>
public class ResolverStatistics
{
    /// <summary>
    /// 解析步驟
    /// </summary>
    public List<string> ResolverSteps { get; set; } = new();

    /// <summary>
    /// 解析步驟數量
    /// </summary>
    public int ResolverCount { get; set; }

    /// <summary>
    /// 最後解析時間
    /// </summary>
    public DateTime LastResolvedAt { get; set; }

    /// <summary>
    /// 解析成功率（需要額外統計）
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 平均解析時間（需要額外統計）
    /// </summary>
    public TimeSpan AverageResolveTime { get; set; }
}

/// <summary>
/// 租戶解析結果
/// </summary>
public class TenantResolveResult
{
    /// <summary>
    /// 解析的租戶
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// 是否解析成功
    /// </summary>
    public bool Success => Tenant != null;

    /// <summary>
    /// 解析方法
    /// </summary>
    public string? ResolveMethod { get; set; }

    /// <summary>
    /// 解析值
    /// </summary>
    public string? ResolveValue { get; set; }

    /// <summary>
    /// 解析時間
    /// </summary>
    public TimeSpan ResolveTime { get; set; }

    /// <summary>
    /// 解析步驟
    /// </summary>
    public List<string> ResolveSteps { get; set; } = new();

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 是否為快取結果
    /// </summary>
    public bool IsCached { get; set; }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static TenantResolveResult CreateSuccess(Tenant tenant, string method, string value, TimeSpan resolveTime, List<string> steps)
    {
        return new TenantResolveResult
        {
            Tenant = tenant,
            ResolveMethod = method,
            ResolveValue = value,
            ResolveTime = resolveTime,
            ResolveSteps = steps
        };
    }

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    public static TenantResolveResult CreateFailure(string errorMessage, List<string> steps)
    {
        return new TenantResolveResult
        {
            ErrorMessage = errorMessage,
            ResolveSteps = steps
        };
    }
}