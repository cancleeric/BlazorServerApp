using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.Services;
using System.Linq.Expressions;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// 租戶解析器單元測試
/// </summary>
public class TenantResolverTests
{
    private readonly MockTenantRepository _mockRepository;
    private readonly TenantContextService _contextService;
    private readonly TenantResolver _resolver;

    public TenantResolverTests()
    {
        _mockRepository = new MockTenantRepository();
        _contextService = new TenantContextService(new MockTenantService());
        _resolver = new TenantResolver(_mockRepository, _contextService);

        // 設定測試資料
        SetupTestData();
    }

    private void SetupTestData()
    {
        var tenant1 = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company 1",
            Slug = "test-company-1",
            Status = TenantStatus.Active,
            PrimaryDomain = "test1.example.com"
        };
        tenant1.AddAllowedDomain("test1.example.com");
        tenant1.AddAllowedDomain("test1-alt.example.com");

        var tenant2 = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Company 2", 
            Slug = "test-company-2",
            Status = TenantStatus.Active,
            PrimaryDomain = "test2.example.com"
        };
        tenant2.AddAllowedDomain("test2.example.com");

        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Default Tenant",
            Slug = "default",
            Status = TenantStatus.Active,
            TenantType = TenantType.Small
        };

        _mockRepository.AddTenant(tenant1);
        _mockRepository.AddTenant(tenant2);
        _mockRepository.AddTenant(defaultTenant);
        _mockRepository.SetDefaultTenant(defaultTenant);
    }

    /// <summary>
    /// 測試從子網域解析租戶
    /// </summary>
    public async Task TestResolveFromSubdomain()
    {
        // Act
        var tenant = await _resolver.ResolveFromSubdomainAsync("test-company-1.example.com");

        // Assert
        if (tenant == null)
            throw new Exception("Should resolve tenant from subdomain");

        if (tenant.Slug != "test-company-1")
            throw new Exception("Should resolve correct tenant by slug");

        // Test with non-existent subdomain
        var nonExistentTenant = await _resolver.ResolveFromSubdomainAsync("non-existent.example.com");
        if (nonExistentTenant != null)
            throw new Exception("Should not resolve non-existent subdomain");

        // Test with system subdomain
        var systemSubdomain = await _resolver.ResolveFromSubdomainAsync("www.example.com");
        if (systemSubdomain != null)
            throw new Exception("Should not resolve system subdomain");
    }

    /// <summary>
    /// 測試從路徑解析租戶
    /// </summary>
    public async Task TestResolveFromPath()
    {
        // Test with /t/ prefix
        var tenant1 = await _resolver.ResolveFromPathAsync("/t/test-company-1/dashboard");
        if (tenant1 == null || tenant1.Slug != "test-company-1")
            throw new Exception("Should resolve tenant from /t/ path");

        // Test with /tenant/ prefix
        var tenant2 = await _resolver.ResolveFromPathAsync("/tenant/test-company-2/settings");
        if (tenant2 == null || tenant2.Slug != "test-company-2")
            throw new Exception("Should resolve tenant from /tenant/ path");

        // Test with /org/ prefix
        var tenant3 = await _resolver.ResolveFromPathAsync("/org/test-company-1/");
        if (tenant3 == null || tenant3.Slug != "test-company-1")
            throw new Exception("Should resolve tenant from /org/ path");

        // Test with invalid path
        var invalidTenant = await _resolver.ResolveFromPathAsync("/invalid/path");
        if (invalidTenant != null)
            throw new Exception("Should not resolve from invalid path");

        // Test with non-existent tenant
        var nonExistentTenant = await _resolver.ResolveFromPathAsync("/t/non-existent/");
        if (nonExistentTenant != null)
            throw new Exception("Should not resolve non-existent tenant from path");
    }

    /// <summary>
    /// 測試從標頭解析租戶
    /// </summary>
    public async Task TestResolveFromHeader()
    {
        var tenant = _mockRepository.GetTenants().First();

        // Test with tenant ID header
        var headers1 = new Dictionary<string, string>
        {
            ["X-Tenant-ID"] = tenant.Id.ToString()
        };
        var resolvedTenant1 = await _resolver.ResolveFromHeaderAsync(headers1);
        if (resolvedTenant1 == null || resolvedTenant1.Id != tenant.Id)
            throw new Exception("Should resolve tenant from ID header");

        // Test with tenant slug header
        var headers2 = new Dictionary<string, string>
        {
            ["X-Tenant-Slug"] = tenant.Slug
        };
        var resolvedTenant2 = await _resolver.ResolveFromHeaderAsync(headers2);
        if (resolvedTenant2 == null || resolvedTenant2.Slug != tenant.Slug)
            throw new Exception("Should resolve tenant from slug header");

        // Test with organization ID header
        var headers3 = new Dictionary<string, string>
        {
            ["X-Organization-ID"] = tenant.Id.ToString()
        };
        var resolvedTenant3 = await _resolver.ResolveFromHeaderAsync(headers3);
        if (resolvedTenant3 == null || resolvedTenant3.Id != tenant.Id)
            throw new Exception("Should resolve tenant from organization ID header");

        // Test with invalid header value
        var invalidHeaders = new Dictionary<string, string>
        {
            ["X-Tenant-ID"] = "invalid-guid"
        };
        var invalidTenant = await _resolver.ResolveFromHeaderAsync(invalidHeaders);
        if (invalidTenant != null)
            throw new Exception("Should not resolve tenant from invalid header");
    }

    /// <summary>
    /// 測試取得預設租戶
    /// </summary>
    public async Task TestGetDefaultTenant()
    {
        // Act
        var defaultTenant = await _resolver.GetDefaultTenantAsync();

        // Assert
        if (defaultTenant == null)
            throw new Exception("Should resolve default tenant");

        if (defaultTenant.Slug != "default")
            throw new Exception("Should resolve correct default tenant");
    }

    /// <summary>
    /// 測試租戶驗證
    /// </summary>
    public async Task TestValidateTenant()
    {
        var activeTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Status = TenantStatus.Active,
            IsDeleted = false,
            SubscriptionPlan = SubscriptionPlan.Professional,
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30)
        };

        var inactiveTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Status = TenantStatus.Suspended,
            IsDeleted = false
        };

        var deletedTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Status = TenantStatus.Active,
            IsDeleted = true
        };

        // Test valid tenant
        var isValidActive = await _resolver.ValidateTenantAsync(activeTenant);
        if (!isValidActive)
            throw new Exception("Active tenant should be valid");

        // Test inactive tenant
        var isValidInactive = await _resolver.ValidateTenantAsync(inactiveTenant);
        if (isValidInactive)
            throw new Exception("Suspended tenant should not be valid");

        // Test deleted tenant
        var isValidDeleted = await _resolver.ValidateTenantAsync(deletedTenant);
        if (isValidDeleted)
            throw new Exception("Deleted tenant should not be valid");

        // Test null tenant
        var isValidNull = await _resolver.ValidateTenantAsync(null!);
        if (isValidNull)
            throw new Exception("Null tenant should not be valid");
    }

    /// <summary>
    /// 測試完整解析流程
    /// </summary>
    public async Task TestCompleteResolveFlow()
    {
        // Test priority order: header > path > subdomain > default
        var tenant = _mockRepository.GetTenants().First();

        // Should prioritize header over path
        var headers = new Dictionary<string, string> { ["X-Tenant-ID"] = tenant.Id.ToString() };
        var resolvedTenant = await _resolver.ResolveAsync(
            host: "wrong-subdomain.example.com",
            path: "/t/wrong-tenant/",
            headers: headers);

        if (resolvedTenant?.Id != tenant.Id)
            throw new Exception("Should prioritize header resolution");

        // Should fall back to path when header fails
        var noHeaderTenant = await _resolver.ResolveAsync(
            host: "wrong-subdomain.example.com", 
            path: "/t/test-company-1/");

        if (noHeaderTenant?.Slug != "test-company-1")
            throw new Exception("Should fall back to path resolution");

        // Should fall back to subdomain when path fails
        var noPathTenant = await _resolver.ResolveAsync(
            host: "test-company-2.example.com",
            path: "/invalid/path/");

        if (noPathTenant?.Slug != "test-company-2")
            throw new Exception("Should fall back to subdomain resolution");

        // Should fall back to default when all else fails
        var defaultTenant = await _resolver.ResolveAsync(
            host: "unknown.example.com",
            path: "/invalid/path/");

        if (defaultTenant?.Slug != "default")
            throw new Exception("Should fall back to default tenant");
    }

    /// <summary>
    /// 執行所有測試
    /// </summary>
    public static async Task<bool> RunAllTests()
    {
        var tests = new TenantResolverTests();
        var testMethods = new Func<Task>[]
        {
            tests.TestResolveFromSubdomain,
            tests.TestResolveFromPath,
            tests.TestResolveFromHeader,
            tests.TestGetDefaultTenant,
            tests.TestValidateTenant,
            tests.TestCompleteResolveFlow
        };

        var passedTests = 0;
        var totalTests = testMethods.Length;

        foreach (var test in testMethods)
        {
            try
            {
                await test();
                passedTests++;
                Console.WriteLine($"✅ {test.Method.Name} - PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {test.Method.Name} - FAILED: {ex.Message}");
            }
        }

        Console.WriteLine($"\n📊 TenantResolver Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}

/// <summary>
/// 模擬租戶儲存庫用於測試
/// </summary>
public class MockTenantRepository : ITenantRepository
{
    private readonly Dictionary<Guid, Tenant> _tenants = new();
    private readonly Dictionary<string, Tenant> _tenantsBySlug = new();
    private readonly Dictionary<string, Tenant> _tenantsByDomain = new();
    private Tenant? _defaultTenant;

    public void AddTenant(Tenant tenant)
    {
        _tenants[tenant.Id] = tenant;
        _tenantsBySlug[tenant.Slug] = tenant;
        
        if (!string.IsNullOrEmpty(tenant.PrimaryDomain))
            _tenantsByDomain[tenant.PrimaryDomain] = tenant;
        
        foreach (var domain in tenant.AllowedDomains)
        {
            _tenantsByDomain[domain] = tenant;
        }
    }

    public void SetDefaultTenant(Tenant tenant)
    {
        _defaultTenant = tenant;
    }

    public IEnumerable<Tenant> GetTenants() => _tenants.Values;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _tenants.TryGetValue(id, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        _tenantsBySlug.TryGetValue(slug, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        _tenantsByDomain.TryGetValue(domain, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_defaultTenant);
    }

    public Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tenants.Values.AsEnumerable());
    }

    public Task<IEnumerable<Tenant>> FindAsync(Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var compiled = predicate.Compile();
        var results = _tenants.Values.Where(compiled);
        return Task.FromResult(results);
    }

    public Task<(IEnumerable<Tenant> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<Tenant, bool>>? predicate = null, Expression<Func<Tenant, object>>? orderBy = null, bool orderByDescending = false, CancellationToken cancellationToken = default)
    {
        var query = _tenants.Values.AsEnumerable();

        if (predicate != null)
        {
            var compiled = predicate.Compile();
            query = query.Where(compiled);
        }

        if (orderBy != null)
        {
            var compiledOrderBy = orderBy.Compile();
            query = orderByDescending ? query.OrderByDescending(compiledOrderBy) : query.OrderBy(compiledOrderBy);
        }

        var totalCount = query.Count();
        var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);

        return Task.FromResult((items, totalCount));
    }

    public Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        AddTenant(tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        AddTenant(tenant);
        return Task.FromResult(tenant);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(id, out var tenant))
        {
            tenant.IsDeleted = true; // Soft delete
        }
        return Task.CompletedTask;
    }

    public Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(id, out var tenant))
        {
            _tenants.Remove(id);
            _tenantsBySlug.Remove(tenant.Slug);
            
            if (!string.IsNullOrEmpty(tenant.PrimaryDomain))
                _tenantsByDomain.Remove(tenant.PrimaryDomain);
                
            foreach (var domain in tenant.AllowedDomains)
            {
                _tenantsByDomain.Remove(domain);
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tenants.ContainsKey(id));
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = _tenantsBySlug.ContainsKey(slug);
        if (exists && excludeId.HasValue && _tenantsBySlug[slug].Id == excludeId.Value)
            exists = false;
        return Task.FromResult(exists);
    }

    public Task<bool> DomainExistsAsync(string domain, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = _tenantsByDomain.ContainsKey(domain);
        if (exists && excludeId.HasValue && _tenantsByDomain[domain].Id == excludeId.Value)
            exists = false;
        return Task.FromResult(exists);
    }

    public Task<IEnumerable<Tenant>> GetChildTenantsAsync(Guid parentTenantId, CancellationToken cancellationToken = default)
    {
        var children = _tenants.Values.Where(t => t.ParentTenantId == parentTenantId);
        return Task.FromResult(children);
    }

    public Task<IEnumerable<Tenant>> GetTenantHierarchyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var hierarchy = new List<Tenant>();
        var currentTenant = _tenants.Values.FirstOrDefault(t => t.Id == tenantId);
        
        while (currentTenant != null)
        {
            hierarchy.Add(currentTenant);
            currentTenant = currentTenant.ParentTenantId.HasValue 
                ? _tenants.Values.FirstOrDefault(t => t.Id == currentTenant.ParentTenantId.Value)
                : null;
        }

        return Task.FromResult(hierarchy.AsEnumerable());
    }

    public Task<IEnumerable<Tenant>> GetActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        var activeTenants = _tenants.Values.Where(t => t.IsActive());
        return Task.FromResult(activeTenants);
    }

    public Task<int> CountAsync(Expression<Func<Tenant, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = _tenants.Values.AsEnumerable();
        
        if (predicate != null)
        {
            var compiled = predicate.Compile();
            query = query.Where(compiled);
        }

        return Task.FromResult(query.Count());
    }

    public Task BatchUpdateStatusAsync(IEnumerable<Guid> tenantIds, TenantStatus status, CancellationToken cancellationToken = default)
    {
        foreach (var tenantId in tenantIds)
        {
            if (_tenants.TryGetValue(tenantId, out var tenant))
            {
                tenant.Status = status;
            }
        }
        return Task.CompletedTask;
    }
}