using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Data;
using EnterpriseIDS.Infrastructure.Data.Repositories;

namespace EnterpriseIDS.Infrastructure.Data.Repositories.Tests;

/// <summary>
/// UserRepository 單元測試（獨立測試項目）
/// </summary>
public class UserRepositoryTests : IDisposable
{
    private readonly EnterpriseIdentityDbContext _context;
    private readonly Mock<ITenantContextService> _mockTenantContextService;
    private readonly UserRepository _userRepository;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public UserRepositoryTests()
    {
        // 設定 In-Memory 資料庫
        var options = new DbContextOptionsBuilder<EnterpriseIdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EnterpriseIdentityDbContext(options);
        
        // 建立 Mock 租戶上下文服務
        _mockTenantContextService = new Mock<ITenantContextService>();
        _mockTenantContextService.Setup(x => x.GetCurrentTenantId()).Returns(_testTenantId);
        _mockTenantContextService.Setup(x => x.IsSuperAdminContext()).Returns(false);
        _mockTenantContextService.Setup(x => x.HasTenantAccess(It.IsAny<Guid>())).Returns(true);

        _userRepository = new UserRepository(_context, _mockTenantContextService.Object);

        // 初始化測試資料
        SeedTestData();
    }

    /// <summary>
    /// 初始化測試資料
    /// </summary>
    private void SeedTestData()
    {
        var testUsers = new[]
        {
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                Username = "testuser1",
                Email = "testuser1@example.com",
                FirstName = "Test",
                LastName = "User1",
                DisplayName = "Test User 1",
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                Username = "testuser2",
                Email = "testuser2@example.com",
                FirstName = "Test",
                LastName = "User2",
                DisplayName = "Test User 2",
                Status = UserStatus.Active,
                LdapDistinguishedName = "CN=TestUser2,OU=Users,DC=example,DC=com",
                LdapObjectGuid = Guid.NewGuid().ToString(),
                IsFromLdap = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(), // 不同租戶
                Username = "otheruser",
                Email = "otheruser@example.com",
                FirstName = "Other",
                LastName = "User",
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.Users.AddRange(testUsers);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByUsernameAsync_應該返回使用者_當使用者存在()
    {
        // Act
        var user = await _userRepository.GetByUsernameAsync("testuser1");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser1", user.Username);
        Assert.Equal("testuser1@example.com", user.Email);
        Assert.Equal(_testTenantId, user.TenantId);
    }

    [Fact]
    public async Task GetByUsernameAsync_應該返回Null_當使用者不存在()
    {
        // Act
        var user = await _userRepository.GetByUsernameAsync("nonexistentuser");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetByUsernameAsync_應該遵循租戶隔離()
    {
        // Act - 嘗試取得其他租戶的使用者
        var user = await _userRepository.GetByUsernameAsync("otheruser");

        // Assert - 因為租戶篩選，應該取不到
        Assert.Null(user);
    }

    [Fact]
    public async Task GetByEmailAsync_應該返回使用者_當使用者存在()
    {
        // Act
        var user = await _userRepository.GetByEmailAsync("testuser1@example.com");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser1", user.Username);
        Assert.Equal("testuser1@example.com", user.Email);
    }

    [Fact]
    public async Task GetByLdapDnAsync_應該返回使用者_當LDAP使用者存在()
    {
        // Act
        var user = await _userRepository.GetByLdapDnAsync("CN=TestUser2,OU=Users,DC=example,DC=com");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser2", user.Username);
        Assert.True(user.IsFromLdap);
    }

    [Fact]
    public async Task CreateAsync_應該建立使用者_使用正確的租戶ID()
    {
        // Arrange
        var newUser = new User
        {
            Username = "newuser",
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User",
            Status = UserStatus.Active
        };

        // Act
        var createdUser = await _userRepository.CreateAsync(newUser);

        // Assert
        Assert.NotNull(createdUser);
        Assert.Equal(_testTenantId, createdUser.TenantId);
        Assert.Equal("newuser", createdUser.Username);
        
        // 驗證資料庫中確實存在
        var userInDb = await _context.Users.FindAsync(createdUser.Id);
        Assert.NotNull(userInDb);
    }

    [Fact]
    public async Task ExistsAsync_應該返回True_當使用者存在()
    {
        // Act
        var exists = await _userRepository.ExistsAsync("testuser1");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_應該返回False_當使用者不存在()
    {
        // Act
        var exists = await _userRepository.ExistsAsync("nonexistentuser");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task EmailExistsAsync_應該返回True_當Email存在()
    {
        // Act
        var exists = await _userRepository.EmailExistsAsync("testuser1@example.com");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task GetCountAsync_應該返回正確的計數()
    {
        // Act
        var count = await _userRepository.GetCountAsync();

        // Assert - 應該只計算當前租戶的使用者（2個）
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task 超級管理員上下文_應該能存取所有租戶()
    {
        // Arrange - 設定為超級管理員上下文
        _mockTenantContextService.Setup(x => x.IsSuperAdminContext()).Returns(true);

        // Act
        var allUsers = await _userRepository.GetAllAsync();

        // Assert - 應該能看到所有租戶的使用者（3個）
        Assert.Equal(3, allUsers.Count());
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}