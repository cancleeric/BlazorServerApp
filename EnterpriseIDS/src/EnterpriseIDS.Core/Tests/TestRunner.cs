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
        Console.WriteLine("🧪 Running EnterpriseIDS Multi-Tenant Architecture Test Suite");
        Console.WriteLine("=" + new string('=', 60));

        var testResults = new List<(string TestSuite, bool Passed)>();

        // 1. 核心實體測試
        Console.WriteLine("\n📦 Testing Core Entities...");
        testResults.Add(("Tenant Entity Tests", TenantTests.RunAllTests()));
        testResults.Add(("User Entity Tests", UserEntityTests.RunAllTests()));
        testResults.Add(("Group Entity Tests", GroupEntityTests.RunAllTests()));
        testResults.Add(("LDAP Configuration Tests", LdapConfigurationTests.RunAllTests()));

        // Integration tests
        Console.WriteLine("\n🔗 Testing Integration Scenarios...");
        testResults.Add(("Multi-Tenant Integration Tests", await MultiTenantIntegrationTests.RunAllIntegrationTests()));
        
        Console.WriteLine("\n⚙️ Core Entity Tests completed.");
        Console.WriteLine("🏗️ Infrastructure tests available separately.");

        // 計算總體結果
        var passedSuites = testResults.Count(r => r.Passed);
        var totalSuites = testResults.Count;
        var allPassed = testResults.All(r => r.Passed);

        // 顯示詳細結果
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("📊 TEST RESULTS SUMMARY");
        Console.WriteLine(new string('=', 60));

        foreach (var (testSuite, passed) in testResults)
        {
            var status = passed ? "✅ PASSED" : "❌ FAILED";
            Console.WriteLine($"{status} - {testSuite}");
        }

        Console.WriteLine($"\n📈 Overall Results: {passedSuites}/{totalSuites} test suites passed");
        var successRate = (double)passedSuites / totalSuites * 100;
        Console.WriteLine($"📊 Success Rate: {successRate:F1}%");
        
        if (allPassed)
        {
            Console.WriteLine("\n🎉 All tests PASSED! Multi-tenant architecture implementation is working correctly.");
        }
        else
        {
            Console.WriteLine("\n❌ Some tests FAILED! Please review the failed test suites above.");
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