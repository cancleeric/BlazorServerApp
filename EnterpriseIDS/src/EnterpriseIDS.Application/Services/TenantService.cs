using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Application.Services;

/// <summary>
/// 租戶服務實作
/// </summary>
public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        ITenantRepository tenantRepository,
        ITenantContextService tenantContextService,
        ILogger<TenantService> logger)
    {
        _tenantRepository = tenantRepository;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    /// <summary>
    /// 根據 ID 取得租戶
    /// </summary>
    public async Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得租戶: {TenantId}", tenantId);
            return await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶時發生錯誤: {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// 根據 Slug 取得租戶
    /// </summary>
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("根據 Slug 取得租戶: {Slug}", slug);
            return await _tenantRepository.GetBySlugAsync(slug, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據 Slug 取得租戶時發生錯誤: {Slug}", slug);
            throw;
        }
    }

    /// <summary>
    /// 根據網域取得租戶
    /// </summary>
    public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("根據網域取得租戶: {Domain}", domain);
            return await _tenantRepository.GetByDomainAsync(domain, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據網域取得租戶時發生錯誤: {Domain}", domain);
            throw;
        }
    }

    /// <summary>
    /// 取得所有租戶
    /// </summary>
    public async Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得所有租戶");
            return await _tenantRepository.GetAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得所有租戶時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 取得分頁租戶列表
    /// </summary>
    public async Task<(IEnumerable<Tenant> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        TenantStatus? status = null,
        TenantType? tenantType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得分頁租戶列表: Page={PageNumber}, Size={PageSize}, Search={SearchTerm}, Status={Status}, Type={TenantType}",
                pageNumber, pageSize, searchTerm, status, tenantType);

            // 建立查詢條件
            System.Linq.Expressions.Expression<Func<Tenant, bool>>? predicate = null;

            if (!string.IsNullOrEmpty(searchTerm) || status.HasValue || tenantType.HasValue)
            {
                predicate = t => true;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    var term = searchTerm.ToLowerInvariant();
                    predicate = t => t.Name.ToLowerInvariant().Contains(term) ||
                                    t.Slug.ToLowerInvariant().Contains(term) ||
                                    (t.ContactEmail != null && t.ContactEmail.ToLowerInvariant().Contains(term));
                }

                if (status.HasValue)
                {
                    var currentPredicate = predicate;
                    predicate = t => currentPredicate.Compile()(t) && t.Status == status.Value;
                }

                if (tenantType.HasValue)
                {
                    var currentPredicate = predicate;
                    predicate = t => currentPredicate.Compile()(t) && t.TenantType == tenantType.Value;
                }
            }

            return await _tenantRepository.GetPagedAsync(
                pageNumber,
                pageSize,
                predicate,
                t => t.CreatedAt,
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得分頁租戶列表時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 建立租戶
    /// </summary>
    public async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("建立新租戶: {TenantName} ({TenantSlug})", tenant.Name, tenant.Slug);

            // 驗證租戶資料
            await ValidateTenantAsync(tenant, true, cancellationToken);

            // 設定預設值
            if (tenant.Id == Guid.Empty)
                tenant.Id = Guid.NewGuid();

            tenant.Slug = tenant.Slug.ToLowerInvariant();
            tenant.CreatedAt = DateTime.UtcNow;

            // 新增租戶
            var createdTenant = await _tenantRepository.AddAsync(tenant, cancellationToken);

            _logger.LogInformation("租戶建立成功: {TenantId}", createdTenant.Id);
            return createdTenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立租戶時發生錯誤: {TenantName}", tenant.Name);
            throw;
        }
    }

    /// <summary>
    /// 更新租戶
    /// </summary>
    public async Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("更新租戶: {TenantId}", tenant.Id);

            // 檢查租戶是否存在
            var existingTenant = await _tenantRepository.GetByIdAsync(tenant.Id, cancellationToken);
            if (existingTenant == null)
            {
                throw new InvalidOperationException($"租戶不存在: {tenant.Id}");
            }

            // 驗證租戶資料
            await ValidateTenantAsync(tenant, false, cancellationToken);

            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() &&
                !_tenantContextService.HasTenantAccess(tenant.Id))
            {
                throw new UnauthorizedAccessException("沒有權限修改此租戶");
            }

            tenant.Slug = tenant.Slug.ToLowerInvariant();
            tenant.UpdatedAt = DateTime.UtcNow;

            var updatedTenant = await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            _logger.LogInformation("租戶更新成功: {TenantId}", tenant.Id);
            return updatedTenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新租戶時發生錯誤: {TenantId}", tenant.Id);
            throw;
        }
    }

    /// <summary>
    /// 刪除租戶（軟刪除）
    /// </summary>
    public async Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("刪除租戶: {TenantId}", tenantId);

            // 檢查租戶是否存在
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant == null)
            {
                throw new InvalidOperationException($"租戶不存在: {tenantId}");
            }

            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() &&
                !_tenantContextService.HasTenantAccess(tenantId))
            {
                throw new UnauthorizedAccessException("沒有權限刪除此租戶");
            }

            // 檢查是否有子租戶
            var childTenants = await _tenantRepository.GetChildTenantsAsync(tenantId, cancellationToken);
            if (childTenants.Any())
            {
                throw new InvalidOperationException("無法刪除有子租戶的租戶，請先處理子租戶");
            }

            await _tenantRepository.DeleteAsync(tenantId, cancellationToken);

            _logger.LogInformation("租戶刪除成功: {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除租戶時發生錯誤: {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// 啟用租戶
    /// </summary>
    public async Task ActivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await UpdateTenantStatusAsync(tenantId, TenantStatus.Active, "啟用", cancellationToken);
    }

    /// <summary>
    /// 暫停租戶
    /// </summary>
    public async Task SuspendAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await UpdateTenantStatusAsync(tenantId, TenantStatus.Suspended, "暫停", cancellationToken);
    }

    /// <summary>
    /// 停用租戶
    /// </summary>
    public async Task DisableAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await UpdateTenantStatusAsync(tenantId, TenantStatus.Disabled, "停用", cancellationToken);
    }

    /// <summary>
    /// 檢查 Slug 是否已存在
    /// </summary>
    public async Task<bool> IsSlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tenantRepository.SlugExistsAsync(slug, excludeTenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查 Slug 是否存在時發生錯誤: {Slug}", slug);
            throw;
        }
    }

    /// <summary>
    /// 檢查網域是否已存在
    /// </summary>
    public async Task<bool> IsDomainExistsAsync(string domain, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tenantRepository.DomainExistsAsync(domain, excludeTenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查網域是否存在時發生錯誤: {Domain}", domain);
            throw;
        }
    }

    /// <summary>
    /// 取得租戶階層
    /// </summary>
    public async Task<IEnumerable<Tenant>> GetTenantHierarchyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tenantRepository.GetTenantHierarchyAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶階層時發生錯誤: {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// 取得子租戶
    /// </summary>
    public async Task<IEnumerable<Tenant>> GetChildTenantsAsync(Guid parentTenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tenantRepository.GetChildTenantsAsync(parentTenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得子租戶時發生錯誤: {ParentTenantId}", parentTenantId);
            throw;
        }
    }

    /// <summary>
    /// 檢查租戶是否有效
    /// </summary>
    public async Task<bool> IsValidTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            return tenant != null && tenant.IsActive() && tenant.HasValidSubscription();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查租戶有效性時發生錯誤: {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// 檢查租戶功能是否啟用
    /// </summary>
    public async Task<bool> IsFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            return tenant?.IsFeatureEnabled(featureName) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查租戶功能時發生錯誤: {TenantId}, {FeatureName}", tenantId, featureName);
            return false;
        }
    }

    /// <summary>
    /// 更新租戶狀態
    /// </summary>
    private async Task UpdateTenantStatusAsync(Guid tenantId, TenantStatus status, string operation, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("{Operation}租戶: {TenantId}", operation, tenantId);

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant == null)
            {
                throw new InvalidOperationException($"租戶不存在: {tenantId}");
            }

            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() &&
                !_tenantContextService.HasTenantAccess(tenantId))
            {
                throw new UnauthorizedAccessException($"沒有權限{operation}此租戶");
            }

            tenant.Status = status;
            tenant.UpdatedAt = DateTime.UtcNow;

            await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            _logger.LogInformation("租戶{Operation}成功: {TenantId}", operation, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "租戶{Operation}時發生錯誤: {TenantId}", operation, tenantId);
            throw;
        }
    }

    /// <summary>
    /// 驗證租戶資料
    /// </summary>
    private async Task ValidateTenantAsync(Tenant tenant, bool isCreate, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        // 基本驗證
        if (string.IsNullOrWhiteSpace(tenant.Name))
            errors.Add("租戶名稱為必填項目");

        if (string.IsNullOrWhiteSpace(tenant.Slug))
            errors.Add("租戶標識符為必填項目");

        // Slug 格式驗證
        if (!string.IsNullOrWhiteSpace(tenant.Slug))
        {
            var slug = tenant.Slug.ToLowerInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9-]+$"))
                errors.Add("租戶標識符只能包含小寫字母、數字和連字符");

            if (slug.StartsWith("-") || slug.EndsWith("-"))
                errors.Add("租戶標識符不能以連字符開頭或結尾");

            if (slug.Length < 2 || slug.Length > 50)
                errors.Add("租戶標識符長度必須在 2-50 個字符之間");
        }

        // 檢查 Slug 唯一性
        if (!string.IsNullOrWhiteSpace(tenant.Slug))
        {
            var excludeId = isCreate ? (Guid?)null : tenant.Id;
            var slugExists = await IsSlugExistsAsync(tenant.Slug, excludeId, cancellationToken);
            if (slugExists)
                errors.Add("租戶標識符已存在");
        }

        // 檢查網域唯一性
        if (!string.IsNullOrWhiteSpace(tenant.PrimaryDomain))
        {
            var excludeId = isCreate ? (Guid?)null : tenant.Id;
            var domainExists = await IsDomainExistsAsync(tenant.PrimaryDomain, excludeId, cancellationToken);
            if (domainExists)
                errors.Add("主要網域已被使用");
        }

        // 電子郵件格式驗證
        if (!string.IsNullOrWhiteSpace(tenant.ContactEmail))
        {
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(tenant.ContactEmail, emailRegex))
                errors.Add("聯絡電子郵件格式不正確");
        }

        // 階層驗證
        if (tenant.ParentTenantId.HasValue)
        {
            if (tenant.ParentTenantId == tenant.Id)
                errors.Add("租戶不能將自己設為父租戶");

            var parentTenant = await _tenantRepository.GetByIdAsync(tenant.ParentTenantId.Value, cancellationToken);
            if (parentTenant == null)
                errors.Add("指定的父租戶不存在");
            else if (parentTenant.Status != TenantStatus.Active)
                errors.Add("父租戶必須處於啟用狀態");
        }

        if (errors.Any())
        {
            throw new ArgumentException($"租戶驗證失敗: {string.Join(", ", errors)}");
        }
    }
}