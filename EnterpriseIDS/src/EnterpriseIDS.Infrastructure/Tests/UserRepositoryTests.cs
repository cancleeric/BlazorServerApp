using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Data;
using EnterpriseIDS.Infrastructure.Data.Repositories;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// UserRepository 單元測試
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
    public async Task GetByUsernameAsync_ShouldReturnUser_WhenUserExists()
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
    public async Task GetByUsernameAsync_ShouldReturnNull_WhenUserNotExists()
    {
        // Act
        var user = await _userRepository.GetByUsernameAsync("nonexistentuser");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetByUsernameAsync_ShouldRespectTenantIsolation()
    {
        // Act - 嘗試取得其他租戶的使用者
        var user = await _userRepository.GetByUsernameAsync("otheruser");

        // Assert - 因為租戶篩選，應該取不到
        Assert.Null(user);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Act
        var user = await _userRepository.GetByEmailAsync("testuser1@example.com");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser1", user.Username);
        Assert.Equal("testuser1@example.com", user.Email);
    }

    [Fact]
    public async Task GetByLdapDnAsync_ShouldReturnUser_WhenLdapUserExists()
    {
        // Act
        var user = await _userRepository.GetByLdapDnAsync("CN=TestUser2,OU=Users,DC=example,DC=com");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testuser2", user.Username);
        Assert.True(user.IsFromLdap);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateUser_WithCorrectTenantId()
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
    public async Task GetActiveUsersAsync_ShouldReturnOnlyActiveUsers()
    {
        // Arrange - 建立一個非活躍使用者
        var inactiveUser = new User
        {
            TenantId = _testTenantId,
            Username = "inactiveuser",
            Email = "inactive@example.com",
            Status = UserStatus.Disabled,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(inactiveUser);
        await _context.SaveChangesAsync();

        // Act
        var activeUsers = await _userRepository.GetActiveUsersAsync();

        // Assert
        Assert.All(activeUsers, user => Assert.Equal(UserStatus.Active, user.Status));
        Assert.DoesNotContain(activeUsers, user => user.Username == "inactiveuser");
    }

    [Fact]
    public async Task GetLdapUsersAsync_ShouldReturnOnlyLdapUsers()
    {
        // Act
        var ldapUsers = await _userRepository.GetLdapUsersAsync();

        // Assert
        Assert.All(ldapUsers, user => Assert.False(string.IsNullOrEmpty(user.LdapDistinguishedName)));
        Assert.Contains(ldapUsers, user => user.Username == "testuser2");
        Assert.DoesNotContain(ldapUsers, user => user.Username == "testuser1");
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenUserExists()
    {
        // Act
        var exists = await _userRepository.ExistsAsync("testuser1");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenUserNotExists()
    {
        // Act
        var exists = await _userRepository.ExistsAsync("nonexistentuser");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task EmailExistsAsync_ShouldReturnTrue_WhenEmailExists()
    {
        // Act
        var exists = await _userRepository.EmailExistsAsync("testuser1@example.com");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnCorrectCount()
    {
        // Act
        var count = await _userRepository.GetCountAsync();

        // Assert - 應該只計算當前租戶的使用者（2個）
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetActiveCountAsync_ShouldReturnCorrectActiveCount()
    {
        // Act
        var activeCount = await _userRepository.GetActiveCountAsync();

        // Assert
        Assert.Equal(2, activeCount);
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldUpdateLoginTime()
    {
        // Arrange
        var user = await _userRepository.GetByUsernameAsync("testuser1");
        Assert.NotNull(user);
        var loginTime = DateTime.UtcNow;

        // Act
        await _userRepository.UpdateLastLoginAsync(user.Id, loginTime);

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.LastLoginAt.HasValue);
        Assert.True((updatedUser.LastLoginAt.Value - loginTime).TotalSeconds < 1);
    }

    [Fact]
    public async Task LockUserAsync_ShouldLockUser()
    {
        // Arrange
        var user = await _userRepository.GetByUsernameAsync("testuser1");
        Assert.NotNull(user);
        var lockoutEndTime = DateTime.UtcNow.AddMinutes(30);

        // Act
        await _userRepository.LockUserAsync(user.Id, lockoutEndTime);

        // Assert
        var lockedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(lockedUser);
        Assert.Equal(UserStatus.Locked, lockedUser.Status);
        Assert.True(lockedUser.LockoutEndAt.HasValue);
    }

    [Fact]
    public async Task UnlockUserAsync_ShouldUnlockUser()
    {
        // Arrange
        var user = await _userRepository.GetByUsernameAsync("testuser1");
        Assert.NotNull(user);
        
        // 先鎖定使用者
        await _userRepository.LockUserAsync(user.Id, DateTime.UtcNow.AddMinutes(30));

        // Act
        await _userRepository.UnlockUserAsync(user.Id);

        // Assert
        var unlockedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unlockedUser);
        Assert.Equal(UserStatus.Active, unlockedUser.Status);
        Assert.Null(unlockedUser.LockoutEndAt);
        Assert.Equal(0, unlockedUser.FailedLoginAttempts);
    }

    [Fact]
    public async Task SuperAdminContext_ShouldAccessAllTenants()
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