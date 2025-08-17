using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// 租戶實體單元測試
/// </summary>
public class TenantTests
{
    /// <summary>
    /// 測試租戶建立
    /// </summary>
    public void TestTenantCreation()
    {
        // Arrange & Act
        var tenant = new Tenant
        {
            Name = "Test Company",
            Slug = "test-company",
            TenantType = TenantType.Enterprise,
            Status = TenantStatus.Active,
            ContactEmail = "admin@test-company.com",
            PrimaryDomain = "test-company.com"
        };

        // Assert
        if (tenant.Name != "Test Company")
            throw new Exception("Tenant name not set correctly");

        if (tenant.Slug != "test-company")
            throw new Exception("Tenant slug not set correctly");

        if (!tenant.IsActive())
            throw new Exception("Tenant should be active");

        if (!tenant.IsDomainAllowed("test-company.com"))
            throw new Exception("Primary domain should be allowed");
    }

    /// <summary>
    /// 測試租戶功能管理
    /// </summary>
    public void TestTenantFeatureManagement()
    {
        // Arrange
        var tenant = new Tenant
        {
            Name = "Feature Test Company",
            Slug = "feature-test",
            Status = TenantStatus.Active
        };

        // Act
        tenant.EnableFeature("advanced-reporting");
        tenant.EnableFeature("api-access");

        // Assert
        if (!tenant.IsFeatureEnabled("advanced-reporting"))
            throw new Exception("Feature should be enabled");

        if (!tenant.IsFeatureEnabled("api-access"))
            throw new Exception("API access feature should be enabled");

        if (tenant.IsFeatureEnabled("non-existent-feature"))
            throw new Exception("Non-existent feature should not be enabled");

        // Test disable feature
        tenant.DisableFeature("advanced-reporting");
        if (tenant.IsFeatureEnabled("advanced-reporting"))
            throw new Exception("Feature should be disabled");
    }

    /// <summary>
    /// 測試租戶配置驗證
    /// </summary>
    public void TestTenantConfigurationValidation()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = Guid.NewGuid(),
            ConfigKey = "test.setting",
            ConfigValue = "test_value",
            ConfigType = "string",
            IsRequired = true
        };

        // Act & Assert
        var (isValid, errorMessage) = config.ValidateValue("valid_value");
        if (!isValid)
            throw new Exception($"Valid value should pass validation: {errorMessage}");

        var (isValidEmpty, errorMessageEmpty) = config.ValidateValue("");
        if (isValidEmpty)
            throw new Exception("Empty value should fail validation for required config");

        // Test typed value
        config.ConfigType = "int";
        config.ConfigValue = "123";
        var intValue = config.GetTypedValue<int>();
        if (intValue != 123)
            throw new Exception("Typed value conversion failed");
    }

    /// <summary>
    /// 測試租戶階層關係
    /// </summary>
    public void TestTenantHierarchy()
    {
        // Arrange
        var parentTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Parent Company",
            Slug = "parent-company",
            Status = TenantStatus.Active
        };

        var childTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Child Company",
            Slug = "child-company",
            Status = TenantStatus.Active,
            ParentTenantId = parentTenant.Id
        };

        // Act
        parentTenant.ChildTenants.Add(childTenant);
        childTenant.ParentTenant = parentTenant;

        // Assert
        if (parentTenant.ChildTenants.Count != 1)
            throw new Exception("Parent should have one child tenant");

        if (childTenant.ParentTenantId != parentTenant.Id)
            throw new Exception("Child tenant should reference parent");

        if (childTenant.ParentTenant?.Id != parentTenant.Id)
            throw new Exception("Child tenant navigation property should be set");
    }

    /// <summary>
    /// 執行所有測試
    /// </summary>
    public static bool RunAllTests()
    {
        var tests = new TenantTests();
        var testMethods = new Action[]
        {
            tests.TestTenantCreation,
            tests.TestTenantFeatureManagement,
            tests.TestTenantConfigurationValidation,
            tests.TestTenantHierarchy
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

        Console.WriteLine($"\n📊 Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}