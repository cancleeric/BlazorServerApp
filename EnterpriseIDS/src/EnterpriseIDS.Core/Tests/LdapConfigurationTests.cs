using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// LDAP 配置實體單元測試
/// </summary>
public class LdapConfigurationTests
{
    /// <summary>
    /// 測試建立 LDAP 配置
    /// </summary>
    public static bool TestCreateLdapConfiguration()
    {
        try
        {
            var config = new LdapConfiguration
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Name = "Test AD Configuration",
                Description = "測試 Active Directory 配置",
                ServerType = LdapServerType.ActiveDirectory,
                ServerUrl = "ldap://test.company.com",
                Port = 389,
                UseSsl = false,
                BaseDn = "DC=test,DC=company,DC=com",
                UserBaseDn = "CN=Users,DC=test,DC=company,DC=com",
                GroupBaseDn = "CN=Groups,DC=test,DC=company,DC=com",
                UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))",
                GroupSearchFilter = "(objectClass=group)",
                UsernameAttribute = "sAMAccountName",
                EmailAttribute = "mail",
                FirstNameAttribute = "givenName",
                LastNameAttribute = "sn",
                DisplayNameAttribute = "displayName",
                AuthenticationType = LdapAuthenticationType.Simple,
                ServiceAccountDn = "CN=ldapservice,CN=Users,DC=test,DC=company,DC=com",
                ServiceAccountPassword = "encrypted_password",
                IsEnabled = true,
                SyncFrequencyMinutes = 60,
                EnableIncrementalSync = true,
                ConflictResolution = ConflictResolutionStrategy.LdapWins,
                SyncGroups = true,
                SyncNestedGroups = true,
                MaxNestingLevel = 10,
                CreatedAt = DateTime.UtcNow
            };

            // 驗證基本屬性
            if (config.Name != "Test AD Configuration")
                throw new Exception("配置名稱設定錯誤");

            if (config.ServerType != LdapServerType.ActiveDirectory)
                throw new Exception("伺服器類型設定錯誤");

            if (config.Port != 389)
                throw new Exception("連接埠設定錯誤");

            Console.WriteLine("✅ TestCreateLdapConfiguration - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestCreateLdapConfiguration - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試 LDAP URL 產生
    /// </summary>
    public static bool TestGetLdapUrl()
    {
        try
        {
            // 測試標準 LDAP
            var config1 = new LdapConfiguration
            {
                ServerUrl = "test.company.com",
                Port = 389,
                UseSsl = false
            };

            var url1 = config1.GetLdapUrl();
            if (url1 != "ldap://test.company.com")
                throw new Exception($"標準 LDAP URL 錯誤: {url1}");

            // 測試 LDAPS
            var config2 = new LdapConfiguration
            {
                ServerUrl = "secure.company.com",
                Port = 636,
                UseSsl = true
            };

            var url2 = config2.GetLdapUrl();
            if (url2 != "ldaps://secure.company.com")
                throw new Exception($"LDAPS URL 錯誤: {url2}");

            // 測試自定義連接埠
            var config3 = new LdapConfiguration
            {
                ServerUrl = "custom.company.com",
                Port = 1389,
                UseSsl = false
            };

            var url3 = config3.GetLdapUrl();
            if (url3 != "ldap://custom.company.com:1389")
                throw new Exception($"自定義連接埠 URL 錯誤: {url3}");

            Console.WriteLine("✅ TestGetLdapUrl - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetLdapUrl - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試配置驗證
    /// </summary>
    public static bool TestIsValidConfiguration()
    {
        try
        {
            // 測試有效配置
            var validConfig = new LdapConfiguration
            {
                ServerUrl = "test.company.com",
                BaseDn = "DC=test,DC=company,DC=com",
                Port = 389,
                AuthenticationType = LdapAuthenticationType.Simple,
                ServiceAccountDn = "CN=service,DC=test,DC=company,DC=com",
                ServiceAccountPassword = "password"
            };

            if (!validConfig.IsValidConfiguration())
                throw new Exception("有效配置驗證失敗");

            // 測試無效配置 - 缺少伺服器 URL
            var invalidConfig1 = new LdapConfiguration
            {
                ServerUrl = "",
                BaseDn = "DC=test,DC=company,DC=com"
            };

            if (invalidConfig1.IsValidConfiguration())
                throw new Exception("應該檢測出伺服器 URL 缺失");

            // 測試無效配置 - 缺少 Base DN
            var invalidConfig2 = new LdapConfiguration
            {
                ServerUrl = "test.company.com",
                BaseDn = ""
            };

            if (invalidConfig2.IsValidConfiguration())
                throw new Exception("應該檢測出 Base DN 缺失");

            Console.WriteLine("✅ TestIsValidConfiguration - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestIsValidConfiguration - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試同步狀態更新
    /// </summary>
    public static bool TestUpdateSyncStatus()
    {
        try
        {
            var config = new LdapConfiguration
            {
                LastSyncStatus = LdapSyncStatus.Never,
                LastSyncAt = null,
                LastSyncError = null
            };

            var testError = "測試錯誤訊息";
            var testStats = "{\"users\": 100, \"groups\": 10}";

            config.UpdateSyncStatus(LdapSyncStatus.Failed, testError, testStats);

            if (config.LastSyncStatus != LdapSyncStatus.Failed)
                throw new Exception("同步狀態更新錯誤");

            if (config.LastSyncError != testError)
                throw new Exception("錯誤訊息更新錯誤");

            if (config.SyncStatistics != testStats)
                throw new Exception("統計資訊更新錯誤");

            if (config.LastSyncAt == null || config.LastSyncAt.Value < DateTime.UtcNow.AddMinutes(-1))
                throw new Exception("同步時間更新錯誤");

            Console.WriteLine("✅ TestUpdateSyncStatus - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestUpdateSyncStatus - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試是否需要同步
    /// </summary>
    public static bool TestShouldSync()
    {
        try
        {
            // 測試從未同步的配置
            var config1 = new LdapConfiguration
            {
                IsEnabled = true,
                LastSyncAt = null,
                SyncFrequencyMinutes = 60
            };

            if (!config1.ShouldSync())
                throw new Exception("從未同步的配置應該需要同步");

            // 測試停用的配置
            var config2 = new LdapConfiguration
            {
                IsEnabled = false,
                LastSyncAt = null,
                SyncFrequencyMinutes = 60
            };

            if (config2.ShouldSync())
                throw new Exception("停用的配置不應該同步");

            // 測試最近同步過的配置
            var config3 = new LdapConfiguration
            {
                IsEnabled = true,
                LastSyncAt = DateTime.UtcNow.AddMinutes(-30),
                SyncFrequencyMinutes = 60
            };

            if (config3.ShouldSync())
                throw new Exception("最近同步過的配置不應該再次同步");

            // 測試需要同步的配置
            var config4 = new LdapConfiguration
            {
                IsEnabled = true,
                LastSyncAt = DateTime.UtcNow.AddMinutes(-90),
                SyncFrequencyMinutes = 60
            };

            if (!config4.ShouldSync())
                throw new Exception("超過同步間隔的配置應該需要同步");

            Console.WriteLine("✅ TestShouldSync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestShouldSync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 執行所有 LDAP 配置測試
    /// </summary>
    public static bool RunAllTests()
    {
        Console.WriteLine("🧪 Running LDAP Configuration Tests");
        Console.WriteLine("=" + new string('=', 40));

        var tests = new Func<bool>[]
        {
            TestCreateLdapConfiguration,
            TestGetLdapUrl,
            TestIsValidConfiguration,
            TestUpdateSyncStatus,
            TestShouldSync
        };

        var passedTests = 0;
        var totalTests = tests.Length;

        foreach (var test in tests)
        {
            if (test())
                passedTests++;
        }

        Console.WriteLine($"\n📊 LDAP Configuration Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}