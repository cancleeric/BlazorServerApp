using EnterpriseIDS.Core.IntegrationTests;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// 測試執行器 - 執行所有單元測試和整合測試
/// </summary>
public class TestRunner
{
    /// <summary>
    /// 執行所有核心模組測試
    /// </summary>
    public static async Task<bool> RunAllCoreTests()
    {
        Console.WriteLine("🧪 Running EnterpriseIDS Core Module Tests");
        Console.WriteLine("=" + new string('=', 50));

        var testResults = new List<bool>();

        // 執行單元測試
        Console.WriteLine("\n📋 Running Tenant Entity Tests...");
        testResults.Add(TenantTests.RunAllTests());

        Console.WriteLine("\n🔗 Running Tenant Context Service Tests...");
        testResults.Add(TenantContextServiceTests.RunAllTests());

        Console.WriteLine("\n🔍 Running Tenant Resolver Tests...");
        testResults.Add(await TenantResolverTests.RunAllTests());

        // 執行整合測試
        Console.WriteLine("\n🔄 Running Multi-Tenant Integration Tests...");
        testResults.Add(await MultiTenantIntegrationTests.RunAllIntegrationTests());

        // 計算總體結果
        var passedSuites = testResults.Count(r => r);
        var totalSuites = testResults.Count;
        var allPassed = testResults.All(r => r);

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine($"📊 Overall Core Module Test Results: {passedSuites}/{totalSuites} test suites passed");
        
        if (allPassed)
        {
            Console.WriteLine("🎉 All core module tests PASSED!");
        }
        else
        {
            Console.WriteLine("❌ Some core module tests FAILED!");
        }

        return allPassed;
    }

    /// <summary>
    /// 程式進入點 - 可以單獨執行測試
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            var success = await RunAllCoreTests();
            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Test execution failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}