using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Tests;

/// <summary>
/// Group 實體單元測試
/// </summary>
public class GroupEntityTests
{
    /// <summary>
    /// 測試建立群組實體
    /// </summary>
    public static bool TestCreateGroup()
    {
        try
        {
            var group = new Group
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Name = "TestGroup",
                DisplayName = "Test Group",
                Description = "測試群組",
                GroupType = GroupType.Security,
                IsSystemGroup = false,
                IsFromLdap = true,
                LdapDistinguishedName = "CN=TestGroup,CN=Groups,DC=company,DC=com",
                LdapObjectGuid = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            // 驗證基本屬性
            if (group.Name != "TestGroup")
                throw new Exception("群組名稱設定錯誤");

            if (group.DisplayName != "Test Group")
                throw new Exception("群組顯示名稱設定錯誤");

            if (group.GroupType != GroupType.Security)
                throw new Exception("群組類型設定錯誤");

            if (!group.IsFromLdap)
                throw new Exception("LDAP 來源標記設定錯誤");

            Console.WriteLine("✅ TestCreateGroup - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestCreateGroup - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試取得顯示名稱
    /// </summary>
    public static bool TestGetDisplayName()
    {
        try
        {
            // 測試有顯示名稱的群組
            var group1 = new Group
            {
                Name = "group1",
                DisplayName = "Group One"
            };

            if (group1.GetDisplayName() != "Group One")
                throw new Exception("應該返回顯示名稱");

            // 測試沒有顯示名稱的群組
            var group2 = new Group
            {
                Name = "group2",
                DisplayName = ""
            };

            if (group2.GetDisplayName() != "group2")
                throw new Exception("應該回退到群組名稱");

            Console.WriteLine("✅ TestGetDisplayName - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetDisplayName - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試根群組檢查
    /// </summary>
    public static bool TestIsRootGroup()
    {
        try
        {
            // 測試根群組
            var rootGroup = new Group
            {
                ParentGroupId = null
            };

            if (!rootGroup.IsRootGroup())
                throw new Exception("根群組檢查失敗");

            // 測試子群組
            var childGroup = new Group
            {
                ParentGroupId = Guid.NewGuid()
            };

            if (childGroup.IsRootGroup())
                throw new Exception("子群組不應該是根群組");

            Console.WriteLine("✅ TestIsRootGroup - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestIsRootGroup - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試群組階層路徑
    /// </summary>
    public static bool TestGetHierarchyPath()
    {
        try
        {
            // 建立群組階層
            var rootGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Root",
                ParentGroup = null,
                ParentGroupId = null
            };

            var middleGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Middle",
                ParentGroup = rootGroup,
                ParentGroupId = rootGroup.Id
            };

            var leafGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Leaf",
                ParentGroup = middleGroup,
                ParentGroupId = middleGroup.Id
            };

            // 測試階層路徑
            var path = leafGroup.GetHierarchyPath();

            if (path.Count != 3)
                throw new Exception($"階層路徑長度錯誤: 預期 3，實際 {path.Count}");

            if (path[0].Name != "Root")
                throw new Exception("階層路徑第一個應該是根群組");

            if (path[1].Name != "Middle")
                throw new Exception("階層路徑第二個應該是中間群組");

            if (path[2].Name != "Leaf")
                throw new Exception("階層路徑第三個應該是葉子群組");

            Console.WriteLine("✅ TestGetHierarchyPath - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetHierarchyPath - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試取得所有子孫群組
    /// </summary>
    public static bool TestGetAllDescendants()
    {
        try
        {
            var parentGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Parent"
            };

            var child1 = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Child1"
            };

            var child2 = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Child2"
            };

            var grandchild = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Grandchild"
            };

            // 建立階層關係
            parentGroup.ChildGroups.Add(child1);
            parentGroup.ChildGroups.Add(child2);
            child1.ChildGroups.Add(grandchild);

            // 測試取得所有子孫
            var descendants = parentGroup.GetAllDescendants().ToList();

            if (descendants.Count != 3)
                throw new Exception($"子孫群組數量錯誤: 預期 3，實際 {descendants.Count}");

            var names = descendants.Select(g => g.Name).ToHashSet();
            if (!names.Contains("Child1") || !names.Contains("Child2") || !names.Contains("Grandchild"))
                throw new Exception("子孫群組內容錯誤");

            Console.WriteLine("✅ TestGetAllDescendants - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetAllDescendants - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試群組包含檢查
    /// </summary>
    public static bool TestContainsGroup()
    {
        try
        {
            var parentGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Parent"
            };

            var childGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Child"
            };

            var otherGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Other"
            };

            // 建立階層關係
            parentGroup.ChildGroups.Add(childGroup);

            // 測試包含自己
            if (!parentGroup.ContainsGroup(parentGroup.Id))
                throw new Exception("群組應該包含自己");

            // 測試包含子群組
            if (!parentGroup.ContainsGroup(childGroup.Id))
                throw new Exception("父群組應該包含子群組");

            // 測試不包含其他群組
            if (parentGroup.ContainsGroup(otherGroup.Id))
                throw new Exception("父群組不應該包含無關群組");

            Console.WriteLine("✅ TestContainsGroup - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestContainsGroup - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試成員管理
    /// </summary>
    public static bool TestMemberManagement()
    {
        try
        {
            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = "TestGroup"
            };

            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            // 添加成員
            group.AddMember(userId1);

            if (group.Members.Count != 1)
                throw new Exception("成員添加失敗");

            var membership1 = group.Members.First();
            if (membership1.UserId != userId1 || !membership1.IsActive)
                throw new Exception("成員資格設定錯誤");

            // 添加有期限的成員
            var expiryDate = DateTime.UtcNow.AddDays(30);
            group.AddMember(userId2, expiryDate);

            if (group.Members.Count != 2)
                throw new Exception("第二個成員添加失敗");

            var membership2 = group.Members.FirstOrDefault(m => m.UserId == userId2);
            if (membership2 == null || membership2.ExpiresAt != expiryDate)
                throw new Exception("有期限成員設定錯誤");

            // 移除成員
            group.RemoveMember(userId1);

            var removedMembership = group.Members.FirstOrDefault(m => m.UserId == userId1);
            if (removedMembership == null || removedMembership.IsActive || !removedMembership.IsDeleted)
                throw new Exception("成員移除失敗");

            Console.WriteLine("✅ TestMemberManagement - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestMemberManagement - FAILED: {ex.Message}");
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
            var group = new Group
            {
                IsFromLdap = true,
                LdapSyncHash = null,
                LastLdapSyncAt = null
            };

            var currentHash = "test_hash_123";

            // 測試需要同步 (首次同步)
            if (!group.RequiresLdapSync(currentHash))
                throw new Exception("首次同步應該需要 LDAP 同步");

            // 更新同步資訊
            group.UpdateLdapSync(currentHash);

            if (group.LdapSyncHash != currentHash)
                throw new Exception("LDAP 同步雜湊更新錯誤");

            if (group.LastLdapSyncAt == null || group.LastLdapSyncAt.Value < DateTime.UtcNow.AddMinutes(-1))
                throw new Exception("LDAP 同步時間更新錯誤");

            // 測試不需要同步 (雜湊相同且時間未過期)
            if (group.RequiresLdapSync(currentHash))
                throw new Exception("雜湊相同且時間未過期時不應該需要同步");

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
    /// 執行所有群組實體測試
    /// </summary>
    public static bool RunAllTests()
    {
        Console.WriteLine("🧪 Running Group Entity Tests");
        Console.WriteLine("=" + new string('=', 30));

        var tests = new Func<bool>[]
        {
            TestCreateGroup,
            TestGetDisplayName,
            TestIsRootGroup,
            TestGetHierarchyPath,
            TestGetAllDescendants,
            TestContainsGroup,
            TestMemberManagement,
            TestLdapSyncFunctionality
        };

        var passedTests = 0;
        var totalTests = tests.Length;

        foreach (var test in tests)
        {
            if (test())
                passedTests++;
        }

        Console.WriteLine($"\n📊 Group Entity Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}