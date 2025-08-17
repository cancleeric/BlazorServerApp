using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// User 實體單元測試
/// </summary>
public class UserEntityTests
{
    /// <summary>
    /// 測試建立使用者實體
    /// </summary>
    public static bool TestCreateUser()
    {
        try
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Username = "testuser",
                Email = "testuser@company.com",
                FirstName = "Test",
                LastName = "User",
                DisplayName = "Test User",
                Department = "IT",
                JobTitle = "Developer",
                Status = UserStatus.Active,
                IsFromLdap = true,
                LdapDistinguishedName = "CN=testuser,CN=Users,DC=company,DC=com",
                LdapObjectGuid = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            // 驗證基本屬性
            if (user.Username != "testuser")
                throw new Exception("使用者名稱設定錯誤");

            if (user.Email != "testuser@company.com")
                throw new Exception("電子郵件設定錯誤");

            if (user.Status != UserStatus.Active)
                throw new Exception("使用者狀態設定錯誤");

            if (!user.IsFromLdap)
                throw new Exception("LDAP 來源標記設定錯誤");

            Console.WriteLine("✅ TestCreateUser - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestCreateUser - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試取得完整名稱
    /// </summary>
    public static bool TestGetFullName()
    {
        try
        {
            // 測試使用顯示名稱
            var user1 = new User
            {
                Username = "user1",
                FirstName = "John",
                LastName = "Doe",
                DisplayName = "Johnny Doe"
            };

            if (user1.GetFullName() != "Johnny Doe")
                throw new Exception("應該返回顯示名稱");

            // 測試使用名字和姓氏
            var user2 = new User
            {
                Username = "user2",
                FirstName = "Jane",
                LastName = "Smith",
                DisplayName = ""
            };

            if (user2.GetFullName() != "Jane Smith")
                throw new Exception("應該返回名字和姓氏組合");

            // 測試回退到使用者名稱
            var user3 = new User
            {
                Username = "user3",
                FirstName = "",
                LastName = "",
                DisplayName = ""
            };

            if (user3.GetFullName() != "user3")
                throw new Exception("應該回退到使用者名稱");

            Console.WriteLine("✅ TestGetFullName - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetFullName - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試使用者狀態檢查
    /// </summary>
    public static bool TestUserStatusChecks()
    {
        try
        {
            // 測試活躍使用者
            var activeUser = new User
            {
                Status = UserStatus.Active,
                IsDeleted = false
            };

            if (!activeUser.IsActive())
                throw new Exception("活躍使用者狀態檢查失敗");

            // 測試停用使用者
            var inactiveUser = new User
            {
                Status = UserStatus.Inactive,
                IsDeleted = false
            };

            if (inactiveUser.IsActive())
                throw new Exception("停用使用者狀態檢查失敗");

            // 測試鎖定使用者
            var lockedUser = new User
            {
                Status = UserStatus.Locked,
                LockoutEndAt = DateTime.UtcNow.AddHours(1)
            };

            if (!lockedUser.IsLocked())
                throw new Exception("鎖定使用者狀態檢查失敗");

            // 測試鎖定時間到期的使用者
            var expiredLockUser = new User
            {
                Status = UserStatus.Active,
                LockoutEndAt = DateTime.UtcNow.AddHours(-1)
            };

            if (expiredLockUser.IsLocked())
                throw new Exception("鎖定時間到期的使用者狀態檢查失敗");

            Console.WriteLine("✅ TestUserStatusChecks - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestUserStatusChecks - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試密碼過期檢查
    /// </summary>
    public static bool TestPasswordExpiration()
    {
        try
        {
            // 測試密碼未過期
            var user1 = new User
            {
                Status = UserStatus.Active,
                PasswordExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            if (user1.IsPasswordExpired())
                throw new Exception("密碼未過期檢查失敗");

            // 測試密碼已過期
            var user2 = new User
            {
                Status = UserStatus.Active,
                PasswordExpiresAt = DateTime.UtcNow.AddDays(-1)
            };

            if (!user2.IsPasswordExpired())
                throw new Exception("密碼已過期檢查失敗");

            // 測試密碼過期狀態
            var user3 = new User
            {
                Status = UserStatus.PasswordExpired
            };

            if (!user3.IsPasswordExpired())
                throw new Exception("密碼過期狀態檢查失敗");

            Console.WriteLine("✅ TestPasswordExpiration - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestPasswordExpiration - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試失敗登入處理
    /// </summary>
    public static bool TestFailedLoginHandling()
    {
        try
        {
            var user = new User
            {
                Status = UserStatus.Active,
                FailedLoginAttempts = 0
            };

            // 增加失敗登入次數
            user.IncrementFailedLoginAttempts(3, 30);
            if (user.FailedLoginAttempts != 1)
                throw new Exception("失敗登入次數增加錯誤");

            user.IncrementFailedLoginAttempts(3, 30);
            if (user.FailedLoginAttempts != 2)
                throw new Exception("失敗登入次數增加錯誤");

            // 達到最大次數應該鎖定
            user.IncrementFailedLoginAttempts(3, 30);
            if (user.Status != UserStatus.Locked)
                throw new Exception("達到最大失敗次數未鎖定");

            if (user.LockedAt == null)
                throw new Exception("鎖定時間未設定");

            if (user.LockoutEndAt == null || user.LockoutEndAt <= user.LockedAt)
                throw new Exception("鎖定結束時間設定錯誤");

            // 重設失敗登入次數
            user.ResetFailedLoginAttempts();
            if (user.FailedLoginAttempts != 0)
                throw new Exception("失敗登入次數重設錯誤");

            if (user.Status != UserStatus.Active)
                throw new Exception("重設後狀態應該為活躍");

            Console.WriteLine("✅ TestFailedLoginHandling - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestFailedLoginHandling - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試 LDAP 同步功能
    /// </summary>
    public static bool TestLdapSyncFunctionality()
    {
        try
        {
            var user = new User
            {
                IsFromLdap = true,
                LdapSyncHash = null,
                LastLdapSyncAt = null
            };

            var currentHash = "test_hash_123";

            // 測試需要同步 (首次同步)
            if (!user.RequiresLdapSync(currentHash))
                throw new Exception("首次同步應該需要 LDAP 同步");

            // 更新同步資訊
            user.UpdateLdapSync(currentHash);

            if (user.LdapSyncHash != currentHash)
                throw new Exception("LDAP 同步雜湊更新錯誤");

            if (user.LastLdapSyncAt == null || user.LastLdapSyncAt.Value < DateTime.UtcNow.AddMinutes(-1))
                throw new Exception("LDAP 同步時間更新錯誤");

            // 測試不需要同步 (雜湊相同且時間未過期)
            if (user.RequiresLdapSync(currentHash))
                throw new Exception("雜湊相同且時間未過期時不應該需要同步");

            // 測試需要同步 (雜湊不同)
            var newHash = "new_hash_456";
            if (!user.RequiresLdapSync(newHash))
                throw new Exception("雜湊不同時應該需要同步");

            // 測試需要同步 (時間過期)
            user.LastLdapSyncAt = DateTime.UtcNow.AddHours(-2);
            if (!user.RequiresLdapSync(currentHash))
                throw new Exception("超過1小時應該需要強制同步");

            Console.WriteLine("✅ TestLdapSyncFunctionality - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestLdapSyncFunctionality - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 執行所有使用者實體測試
    /// </summary>
    public static bool RunAllTests()
    {
        Console.WriteLine("🧪 Running User Entity Tests");
        Console.WriteLine("=" + new string('=', 30));

        var tests = new Func<bool>[]
        {
            TestCreateUser,
            TestGetFullName,
            TestUserStatusChecks,
            TestPasswordExpiration,
            TestFailedLoginHandling,
            TestLdapSyncFunctionality
        };

        var passedTests = 0;
        var totalTests = tests.Length;

        foreach (var test in tests)
        {
            if (test())
                passedTests++;
        }

        Console.WriteLine($"\n📊 User Entity Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}