using Microsoft.Extensions.Logging;
using Moq;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;
using EnterpriseIDS.Infrastructure.Ldap;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// LDAP 服務單元測試
/// </summary>
public class LdapServiceTests
{
    private readonly Mock<ILogger<LdapService>> _mockLogger;
    private readonly Mock<ILdapConfigurationService> _mockConfigService;
    private readonly LdapService _ldapService;

    public LdapServiceTests()
    {
        _mockLogger = new Mock<ILogger<LdapService>>();
        _mockConfigService = new Mock<ILdapConfigurationService>();
        _ldapService = new LdapService(_mockLogger.Object, _mockConfigService.Object);
    }

    /// <summary>
    /// 測試 LDAP 連線測試功能
    /// </summary>
    public async Task<bool> TestConnectionTestAsync()
    {
        try
        {
            var configuration = new LdapConfiguration
            {
                Id = Guid.NewGuid(),
                Name = "Test Config",
                ServerUrl = "ldap://test.company.com",
                Port = 389,
                UseSsl = false,
                BaseDn = "DC=test,DC=company,DC=com",
                AuthenticationType = LdapAuthenticationType.Anonymous
            };

            // 測試無效配置 (應該失敗)
            var invalidConfig = new LdapConfiguration
            {
                ServerUrl = "invalid://nonexistent",
                Port = 389
            };

            var result = await _ldapService.TestConnectionAsync(invalidConfig);

            // 應該會失敗，但不會拋出異常
            if (result.Success)
                throw new Exception("無效配置的連線測試應該失敗");

            if (string.IsNullOrEmpty(result.ErrorMessage))
                throw new Exception("失敗的連線測試應該有錯誤訊息");

            Console.WriteLine("✅ TestConnectionTestAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestConnectionTestAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試使用者認證功能
    /// </summary>
    public async Task<bool> TestAuthenticateAsync()
    {
        try
        {
            var configId = Guid.NewGuid();
            var configuration = new LdapConfiguration
            {
                Id = configId,
                ServerUrl = "ldap://test.company.com",
                Port = 389,
                BaseDn = "DC=test,DC=company,DC=com",
                UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))",
                AuthenticationType = LdapAuthenticationType.Anonymous
            };

            _mockConfigService
                .Setup(s => s.GetConfigurationByIdAsync(configId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuration);

            // 測試不存在的配置
            var noConfigResult = await _ldapService.AuthenticateAsync("testuser", "password", Guid.NewGuid());
            
            if (noConfigResult.Success)
                throw new Exception("不存在的配置認證應該失敗");

            if (noConfigResult.ErrorMessage != "LDAP configuration not found")
                throw new Exception("錯誤訊息不正確");

            Console.WriteLine("✅ TestAuthenticateAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestAuthenticateAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試健康檢查功能
    /// </summary>
    public async Task<bool> TestCheckHealthAsync()
    {
        try
        {
            var configId = Guid.NewGuid();

            // 測試不存在的配置
            _mockConfigService
                .Setup(s => s.GetConfigurationByIdAsync(configId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LdapConfiguration?)null);

            var result = await _ldapService.CheckHealthAsync(configId);

            if (result.IsHealthy)
                throw new Exception("不存在的配置健康檢查應該失敗");

            if (result.Status != "Configuration Not Found")
                throw new Exception("健康檢查狀態錯誤");

            if (!result.ErrorMessage!.Contains("not found"))
                throw new Exception("健康檢查錯誤訊息不正確");

            Console.WriteLine("✅ TestCheckHealthAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestCheckHealthAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試清理過期資源功能
    /// </summary>
    public async Task<bool> TestCleanupExpiredResourcesAsync()
    {
        try
        {
            // 這個方法應該能正常執行而不拋出異常
            await _ldapService.CleanupExpiredResourcesAsync();

            Console.WriteLine("✅ TestCleanupExpiredResourcesAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestCleanupExpiredResourcesAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試使用者搜尋功能
    /// </summary>
    public async Task<bool> TestSearchUsersAsync()
    {
        try
        {
            var configId = Guid.NewGuid();

            // 測試不存在的配置
            _mockConfigService
                .Setup(s => s.GetConfigurationByIdAsync(configId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LdapConfiguration?)null);

            var users = await _ldapService.SearchUsersAsync("test", configId);

            if (users.Any())
                throw new Exception("不存在的配置搜尋應該返回空結果");

            Console.WriteLine("✅ TestSearchUsersAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestSearchUsersAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試取得統計資訊功能
    /// </summary>
    public async Task<bool> TestGetStatisticsAsync()
    {
        try
        {
            var configId = Guid.NewGuid();

            // 測試連線統計
            var connectionStats = await _ldapService.GetConnectionStatisticsAsync(configId);
            
            if (!connectionStats.ContainsKey("ConnectionPoolSize"))
                throw new Exception("連線統計應該包含連線池大小");

            if (!connectionStats.ContainsKey("Status"))
                throw new Exception("連線統計應該包含狀態");

            // 測試同步統計
            var syncStats = await _ldapService.GetSyncStatisticsAsync(configId);
            
            if (!syncStats.ContainsKey("Status"))
                throw new Exception("同步統計應該包含狀態");

            Console.WriteLine("✅ TestGetStatisticsAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetStatisticsAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 執行所有 LDAP 服務測試
    /// </summary>
    public static async Task<bool> RunAllTestsAsync()
    {
        Console.WriteLine("🧪 Running LDAP Service Tests");
        Console.WriteLine("=" + new string('=', 35));

        var testInstance = new LdapServiceTests();
        var tests = new Func<Task<bool>>[]
        {
            testInstance.TestConnectionTestAsync,
            testInstance.TestAuthenticateAsync,
            testInstance.TestCheckHealthAsync,
            testInstance.TestCleanupExpiredResourcesAsync,
            testInstance.TestSearchUsersAsync,
            testInstance.TestGetStatisticsAsync
        };

        var passedTests = 0;
        var totalTests = tests.Length;

        foreach (var test in tests)
        {
            try
            {
                if (await test())
                    passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
            }
        }

        Console.WriteLine($"\n📊 LDAP Service Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}