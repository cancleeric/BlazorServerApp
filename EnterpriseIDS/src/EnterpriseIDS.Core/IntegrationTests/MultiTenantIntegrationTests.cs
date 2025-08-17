using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Services;
using EnterpriseIDS.Core.Tests;

namespace EnterpriseIDS.Core.IntegrationTests;

/// <summary>
/// 多租戶架構整合測試
/// </summary>
public class MultiTenantIntegrationTests
{
    private readonly MockTenantRepository _repository;
    private readonly MockTenantService _tenantService;
    private readonly TenantContextService _contextService;
    private readonly TenantResolver _resolver;

    public MultiTenantIntegrationTests()
    {
        _repository = new MockTenantRepository();
        _tenantService = new MockTenantService();
        _contextService = new TenantContextService(_tenantService);
        _resolver = new TenantResolver(_repository, _contextService);

        SetupIntegrationTestData();
    }

    private void SetupIntegrationTestData()
    {
        // 建立父租戶
        var parentTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Parent Corporation",
            Slug = "parent-corp",
            Status = TenantStatus.Active,
            TenantType = TenantType.Enterprise,
            PrimaryDomain = "parent.example.com",
            SubscriptionPlan = SubscriptionPlan.Enterprise,
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-365),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(365)
        };
        parentTenant.AddAllowedDomain("parent.example.com");
        parentTenant.AddAllowedDomain("corp.example.com");
        parentTenant.EnableFeature("advanced-analytics");
        parentTenant.EnableFeature("api-access");
        parentTenant.EnableFeature("multi-tenant-management");

        // 建立子租戶 1
        var childTenant1 = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Subsidiary A",
            Slug = "subsidiary-a",
            Status = TenantStatus.Active,
            TenantType = TenantType.Medium,
            ParentTenantId = parentTenant.Id,
            PrimaryDomain = "suba.example.com",
            SubscriptionPlan = SubscriptionPlan.Professional,
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-200),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(165)
        };
        childTenant1.AddAllowedDomain("suba.example.com");
        childTenant1.EnableFeature("basic-reporting");
        childTenant1.EnableFeature("api-access");

        // 建立子租戶 2
        var childTenant2 = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Subsidiary B",
            Slug = "subsidiary-b", 
            Status = TenantStatus.Active,
            TenantType = TenantType.Small,
            ParentTenantId = parentTenant.Id,
            PrimaryDomain = "subb.example.com",
            SubscriptionPlan = SubscriptionPlan.Basic,
            SubscriptionStartDate = DateTime.UtcNow.AddDays(-100),
            SubscriptionEndDate = DateTime.UtcNow.AddDays(265)
        };
        childTenant2.AddAllowedDomain("subb.example.com");
        childTenant2.EnableFeature("basic-reporting");

        // 建立試用租戶
        var trialTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Trial Company",
            Slug = "trial-company",
            Status = TenantStatus.Active,
            TenantType = TenantType.Small,
            PrimaryDomain = "trial.example.com",
            SubscriptionPlan = SubscriptionPlan.Free,
            TrialEndDate = DateTime.UtcNow.AddDays(14)
        };
        trialTenant.AddAllowedDomain("trial.example.com");

        // 建立預設租戶
        var defaultTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Default System",
            Slug = "default",
            Status = TenantStatus.Active,
            TenantType = TenantType.Small,
            SubscriptionPlan = SubscriptionPlan.Free
        };

        // 設定階層關係
        parentTenant.ChildTenants.Add(childTenant1);
        parentTenant.ChildTenants.Add(childTenant2);
        childTenant1.ParentTenant = parentTenant;
        childTenant2.ParentTenant = parentTenant;

        // 加入到儲存庫
        _repository.AddTenant(parentTenant);
        _repository.AddTenant(childTenant1);
        _repository.AddTenant(childTenant2);
        _repository.AddTenant(trialTenant);
        _repository.AddTenant(defaultTenant);
        _repository.SetDefaultTenant(defaultTenant);

        // 加入到服務
        _tenantService.CreateAsync(parentTenant);
        _tenantService.CreateAsync(childTenant1);
        _tenantService.CreateAsync(childTenant2);
        _tenantService.CreateAsync(trialTenant);
        _tenantService.CreateAsync(defaultTenant);
    }

    /// <summary>
    /// 測試完整的租戶解析和上下文設定流程
    /// </summary>
    public async Task TestCompleteRequestResolutionFlow()
    {
        // 模擬來自子租戶的 HTTP 請求
        var headers = new Dictionary<string, string>
        {
            ["X-Tenant-Slug"] = "subsidiary-a"
        };

        // Act 1: 解析租戶
        var resolvedTenant = await _resolver.ResolveAsync(
            host: "suba.example.com",
            path: "/dashboard",
            headers: headers);

        if (resolvedTenant == null)
            throw new Exception("應該能成功解析子租戶");

        if (resolvedTenant.Slug != "subsidiary-a")
            throw new Exception("應該解析到正確的子租戶");

        // Act 2: 設定租戶上下文
        var tenantContext = TenantContext.FromTenant(resolvedTenant);
        _contextService.SetCurrentTenantContext(tenantContext);

        // Act 3: 驗證上下文
        var currentTenantId = _contextService.GetCurrentTenantId();
        if (currentTenantId != resolvedTenant.Id)
            throw new Exception("租戶上下文應該設定正確");

        // Act 4: 檢查權限
        var hasAccessToSelf = _contextService.HasTenantAccess(resolvedTenant.Id);
        if (!hasAccessToSelf)
            throw new Exception("租戶應該能存取自己");

        var parentTenant = _repository.GetTenants().First(t => t.Slug == "parent-corp");
        var hasAccessToParent = _contextService.HasTenantAccess(parentTenant.Id);
        // 目前實作中 IsParentTenant 回傳 false，所以這裡應該是 false
        if (hasAccessToParent)
            throw new Exception("子租戶目前不應該能存取父租戶");

        // Act 5: 檢查功能啟用
        var hasApiAccess = await _contextService.IsFeatureEnabledAsync("api-access");
        if (!hasApiAccess)
            throw new Exception("子租戶應該啟用 API 存取功能");

        var hasAdvancedAnalytics = await _contextService.IsFeatureEnabledAsync("advanced-analytics");
        if (hasAdvancedAnalytics)
            throw new Exception("子租戶不應該有高級分析功能");
    }

    /// <summary>
    /// 測試租戶階層管理
    /// </summary>
    public async Task TestTenantHierarchyManagement()
    {
        var parentTenant = _repository.GetTenants().First(t => t.Slug == "parent-corp");
        var childTenant = _repository.GetTenants().First(t => t.Slug == "subsidiary-a");

        // 測試取得子租戶
        var childTenants = await _tenantService.GetChildTenantsAsync(parentTenant.Id);
        if (childTenants.Count() != 2)
            throw new Exception("父租戶應該有兩個子租戶");

        // 測試取得租戶階層
        var hierarchy = await _tenantService.GetTenantHierarchyAsync(childTenant.Id);
        var hierarchyList = hierarchy.ToList();
        
        if (hierarchyList.Count != 2)
            throw new Exception("租戶階層應該包含子租戶和父租戶");

        if (hierarchyList[0].Id != childTenant.Id)
            throw new Exception("階層第一個應該是子租戶自己");

        if (hierarchyList[1].Id != parentTenant.Id)
            throw new Exception("階層第二個應該是父租戶");
    }

    /// <summary>
    /// 測試租戶訂閱和權限驗證
    /// </summary>
    public async Task TestTenantSubscriptionAndPermissions()
    {
        var trialTenant = _repository.GetTenants().First(t => t.Slug == "trial-company");
        var enterpriseTenant = _repository.GetTenants().First(t => t.Slug == "parent-corp");

        // 測試試用租戶驗證
        var isTrialValid = await _resolver.ValidateTenantAsync(trialTenant);
        if (!isTrialValid)
            throw new Exception("試用租戶應該是有效的");

        // 測試企業租戶驗證
        var isEnterpriseValid = await _resolver.ValidateTenantAsync(enterpriseTenant);
        if (!isEnterpriseValid)
            throw new Exception("企業租戶應該是有效的");

        // 測試功能權限
        var enterpriseHasAdvanced = await _tenantService.IsFeatureEnabledAsync(
            enterpriseTenant.Id, "advanced-analytics");
        if (!enterpriseHasAdvanced)
            throw new Exception("企業租戶應該有高級分析功能");

        var trialHasAdvanced = await _tenantService.IsFeatureEnabledAsync(
            trialTenant.Id, "advanced-analytics");
        if (trialHasAdvanced)
            throw new Exception("試用租戶不應該有高級分析功能");
    }

    /// <summary>
    /// 測試租戶解析優先順序
    /// </summary>
    public async Task TestTenantResolutionPriority()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Tenant-ID"] = _repository.GetTenants().First(t => t.Slug == "parent-corp").Id.ToString()
        };

        // 標頭應該優先於子網域和路徑
        var tenant = await _resolver.ResolveAsync(
            host: "subsidiary-a.example.com", // 這應該解析到 subsidiary-a
            path: "/t/subsidiary-b/dashboard", // 這應該解析到 subsidiary-b
            headers: headers); // 但這應該解析到 parent-corp

        if (tenant == null || tenant.Slug != "parent-corp")
            throw new Exception("應該優先使用標頭解析租戶");

        // 測試無標頭時的路徑優先於子網域
        var tenantFromPath = await _resolver.ResolveAsync(
            host: "subsidiary-a.example.com",
            path: "/t/subsidiary-b/dashboard");

        if (tenantFromPath == null || tenantFromPath.Slug != "subsidiary-b")
            throw new Exception("應該優先使用路徑解析租戶");

        // 測試回退到子網域
        var tenantFromSubdomain = await _resolver.ResolveAsync(
            host: "subsidiary-a.example.com",
            path: "/invalid/path");

        if (tenantFromSubdomain == null || tenantFromSubdomain.Slug != "subsidiary-a")
            throw new Exception("應該回退到子網域解析租戶");
    }

    /// <summary>
    /// 測試租戶上下文管理器的隔離性
    /// </summary>
    public void TestTenantContextIsolation()
    {
        var tenant1 = _repository.GetTenants().First(t => t.Slug == "subsidiary-a");
        var tenant2 = _repository.GetTenants().First(t => t.Slug == "subsidiary-b");

        // 設定初始上下文
        _contextService.SetCurrentTenant(tenant1.Id);
        var initialTenantId = _contextService.GetCurrentTenantId();

        if (initialTenantId != tenant1.Id)
            throw new Exception("初始上下文應該設定正確");

        // 使用上下文管理器臨時切換
        using (var manager = _contextService.CreateContextManager(tenant2.Id))
        {
            var temporaryTenantId = _contextService.GetCurrentTenantId();
            if (temporaryTenantId != tenant2.Id)
                throw new Exception("臨時上下文應該切換正確");
        }

        // 驗證上下文已恢復
        var restoredTenantId = _contextService.GetCurrentTenantId();
        if (restoredTenantId != tenant1.Id)
            throw new Exception("上下文應該恢復到原始狀態");
    }

    /// <summary>
    /// 執行所有整合測試
    /// </summary>
    public static async Task<bool> RunAllIntegrationTests()
    {
        var tests = new MultiTenantIntegrationTests();
        var testMethods = new Func<Task>[]
        {
            tests.TestCompleteRequestResolutionFlow,
            tests.TestTenantHierarchyManagement,
            tests.TestTenantSubscriptionAndPermissions,
            tests.TestTenantResolutionPriority
        };

        var syncTestMethods = new Action[]
        {
            tests.TestTenantContextIsolation
        };

        var passedTests = 0;
        var totalTests = testMethods.Length + syncTestMethods.Length;

        // 執行異步測試
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

        // 執行同步測試
        foreach (var test in syncTestMethods)
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

        Console.WriteLine($"\n📊 Multi-Tenant Integration Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}