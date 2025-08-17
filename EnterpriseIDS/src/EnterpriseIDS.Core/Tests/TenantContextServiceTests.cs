using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.Services;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// 租戶上下文服務單元測試
/// </summary>
public class TenantContextServiceTests
{
    private readonly MockTenantService _mockTenantService;
    private readonly TenantContextService _contextService;

    public TenantContextServiceTests()
    {
        _mockTenantService = new MockTenantService();
        _contextService = new TenantContextService(_mockTenantService);
    }

    /// <summary>
    /// 測試設定和取得當前租戶 ID
    /// </summary>
    public void TestSetAndGetCurrentTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        _contextService.SetCurrentTenant(tenantId);
        var retrievedTenantId = _contextService.GetCurrentTenantId();

        // Assert
        if (retrievedTenantId != tenantId)
            throw new Exception("Retrieved tenant ID should match the set tenant ID");
    }

    /// <summary>
    /// 測試租戶上下文管理
    /// </summary>
    public void TestTenantContextManagement()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext
        {
            TenantId = tenantId,
            TenantSlug = "test-tenant",
            TenantName = "Test Tenant",
            IsActive = true
        };

        // Act
        _contextService.SetCurrentTenantContext(tenantContext);
        var retrievedContext = _contextService.GetCurrentTenantContext();

        // Assert
        if (retrievedContext == null)
            throw new Exception("Retrieved context should not be null");

        if (retrievedContext.TenantId != tenantId)
            throw new Exception("Retrieved context tenant ID should match");

        if (retrievedContext.TenantSlug != "test-tenant")
            throw new Exception("Retrieved context tenant slug should match");
    }

    /// <summary>
    /// 測試超級管理員上下文檢查
    /// </summary>
    public void TestSuperAdminContextCheck()
    {
        // Arrange & Act - 無租戶上下文時應該為超級管理員
        _contextService.ClearCurrentTenant();
        var isSuperAdminWithoutContext = _contextService.IsSuperAdminContext();

        // Assert
        if (!isSuperAdminWithoutContext)
            throw new Exception("Should be super admin when no tenant context is set");

        // Arrange & Act - 設定非超級管理員上下文
        var normalContext = new TenantContext
        {
            TenantId = Guid.NewGuid(),
            IsSuperAdmin = false
        };
        _contextService.SetCurrentTenantContext(normalContext);
        var isSuperAdminWithNormalContext = _contextService.IsSuperAdminContext();

        // Assert
        if (isSuperAdminWithNormalContext)
            throw new Exception("Should not be super admin with normal tenant context");

        // Arrange & Act - 設定超級管理員上下文
        var superAdminContext = new TenantContext
        {
            TenantId = Guid.NewGuid(),
            IsSuperAdmin = true
        };
        _contextService.SetCurrentTenantContext(superAdminContext);
        var isSuperAdminWithSuperContext = _contextService.IsSuperAdminContext();

        // Assert
        if (!isSuperAdminWithSuperContext)
            throw new Exception("Should be super admin with super admin context");
    }

    /// <summary>
    /// 測試租戶存取權限檢查
    /// </summary>
    public void TestTenantAccessCheck()
    {
        // Arrange
        var currentTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        var context = new TenantContext
        {
            TenantId = currentTenantId,
            IsSuperAdmin = false
        };

        // Act
        _contextService.SetCurrentTenantContext(context);

        // Assert - 當前租戶可以存取自己
        var hasAccessToSelf = _contextService.HasTenantAccess(currentTenantId);
        if (!hasAccessToSelf)
            throw new Exception("Current tenant should have access to itself");

        // Assert - 當前租戶不能存取其他租戶
        var hasAccessToOther = _contextService.HasTenantAccess(otherTenantId);
        if (hasAccessToOther)
            throw new Exception("Current tenant should not have access to other tenants");

        // Assert - 超級管理員可以存取所有租戶
        context.IsSuperAdmin = true;
        _contextService.SetCurrentTenantContext(context);
        var superAdminAccessToOther = _contextService.HasTenantAccess(otherTenantId);
        if (!superAdminAccessToOther)
            throw new Exception("Super admin should have access to all tenants");
    }

    /// <summary>
    /// 測試上下文管理器
    /// </summary>
    public void TestTenantContextManager()
    {
        // Arrange
        var originalTenantId = Guid.NewGuid();
        var temporaryTenantId = Guid.NewGuid();

        var originalContext = new TenantContext { TenantId = originalTenantId };
        var temporaryContext = new TenantContext { TenantId = temporaryTenantId };

        _contextService.SetCurrentTenantContext(originalContext);

        // Act & Assert
        using (var manager = _contextService.CreateContextManager(temporaryContext))
        {
            var currentContext = _contextService.GetCurrentTenantContext();
            if (currentContext?.TenantId != temporaryTenantId)
                throw new Exception("Temporary context should be active within manager scope");
        }

        // Assert - 原始上下文應該被恢復
        var restoredContext = _contextService.GetCurrentTenantContext();
        if (restoredContext?.TenantId != originalTenantId)
            throw new Exception("Original context should be restored after manager disposal");
    }

    /// <summary>
    /// 測試解析步驟管理
    /// </summary>
    public void TestResolverStepManagement()
    {
        // Arrange
        _contextService.ClearResolverStack();

        // Act
        _contextService.AddResolverStep("step1");
        _contextService.AddResolverStep("step2");
        _contextService.AddResolverStep("step3");

        // Assert
        var steps = _contextService.GetResolverStack();
        if (steps.Count != 3)
            throw new Exception("Should have 3 resolver steps");

        if (steps[0] != "step1" || steps[1] != "step2" || steps[2] != "step3")
            throw new Exception("Resolver steps should be in correct order");

        // Act - 清除步驟
        _contextService.ClearResolverStack();
        var clearedSteps = _contextService.GetResolverStack();

        // Assert
        if (clearedSteps.Count != 0)
            throw new Exception("Resolver stack should be empty after clearing");
    }

    /// <summary>
    /// 執行所有測試
    /// </summary>
    public static bool RunAllTests()
    {
        var tests = new TenantContextServiceTests();
        var testMethods = new Action[]
        {
            tests.TestSetAndGetCurrentTenantId,
            tests.TestTenantContextManagement,
            tests.TestSuperAdminContextCheck,
            tests.TestTenantAccessCheck,
            tests.TestTenantContextManager,
            tests.TestResolverStepManagement
        };

        var passedTests = 0;
        var totalTests = testMethods.Length;

        foreach (var test in testMethods)
        {
            try
            {
                test();
                passedTests++;
                Console.WriteLine($"✅ {test.Method.Name} - PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {test.Method.Name} - FAILED: {ex.Message}");
            }
        }

        Console.WriteLine($"\n📊 TenantContextService Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}

/// <summary>
/// 模擬租戶服務用於測試
/// </summary>
public class MockTenantService : ITenantService
{
    private readonly Dictionary<Guid, Tenant> _tenants = new();

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenant = _tenants.Values.FirstOrDefault(t => t.Slug == slug);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        var tenant = _tenants.Values.FirstOrDefault(t => t.IsDomainAllowed(domain));
        return Task.FromResult(tenant);
    }

    public Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tenants.Values.AsEnumerable());
    }

    public Task<(IEnumerable<Tenant> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, TenantStatus? status = null, TenantType? tenantType = null, CancellationToken cancellationToken = default)
    {
        var query = _tenants.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(searchTerm))
            query = query.Where(t => t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (tenantType.HasValue)
            query = query.Where(t => t.TenantType == tenantType.Value);

        var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        return Task.FromResult((items, query.Count()));
    }

    public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (tenant.Id == Guid.Empty)
            tenant.Id = Guid.NewGuid();
        
        _tenants[tenant.Id] = tenant;
        return Task.FromResult(tenant);
    }

    public Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _tenants[tenant.Id] = tenant;
        return Task.FromResult(tenant);
    }

    public Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _tenants.Remove(tenantId);
        return Task.CompletedTask;
    }

    public Task ActivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            tenant.Activate();
        }
        return Task.CompletedTask;
    }

    public Task SuspendAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            tenant.Suspend();
        }
        return Task.CompletedTask;
    }

    public Task DisableAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            tenant.Disable();
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsSlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        var exists = _tenants.Values.Any(t => t.Slug == slug && t.Id != excludeTenantId);
        return Task.FromResult(exists);
    }

    public Task<bool> IsDomainExistsAsync(string domain, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        var exists = _tenants.Values.Any(t => t.IsDomainAllowed(domain) && t.Id != excludeTenantId);
        return Task.FromResult(exists);
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

    public Task<IEnumerable<Tenant>> GetChildTenantsAsync(Guid parentTenantId, CancellationToken cancellationToken = default)
    {
        var children = _tenants.Values.Where(t => t.ParentTenantId == parentTenantId);
        return Task.FromResult(children);
    }

    public Task<bool> IsValidTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            return Task.FromResult(tenant.IsActive());
        }
        return Task.FromResult(false);
    }

    public Task<bool> IsFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant))
        {
            return Task.FromResult(tenant.IsFeatureEnabled(featureName));
        }
        return Task.FromResult(false);
    }
}