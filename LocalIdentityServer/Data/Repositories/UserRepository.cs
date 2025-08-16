using Microsoft.EntityFrameworkCore;
using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 使用者 Repository 實作 - 遵循單一責任原則 (SRP)
/// 使用 EF Core 進行資料存取，包含適當的錯誤處理和空值檢查
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly LocalIdentityDbContext _context;

    public UserRepository(LocalIdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<UserEntity?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("User ID cannot be null or empty", nameof(id));

        try
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving user by ID: {id}", ex);
        }
    }

    public async Task<UserEntity?> GetByUserNameAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("UserName cannot be null or empty", nameof(userName));

        try
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving user by username: {userName}", ex);
        }
    }

    public async Task<UserEntity?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));

        try
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving user by email: {email}", ex);
        }
    }

    public async Task<UserEntity> AddAsync(UserEntity user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(user.Id))
            throw new ArgumentException("User ID is required", nameof(user));

        if (string.IsNullOrWhiteSpace(user.UserName))
            throw new ArgumentException("UserName is required", nameof(user));

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new ArgumentException("Email is required", nameof(user));

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error adding user: {user.UserName}", ex);
        }
    }

    public async Task UpdateAsync(UserEntity user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        try
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error updating user: {user.Id}", ex);
        }
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error updating last login for user: {userId}", ex);
        }
    }

    public async Task<bool> ExistsByUserNameAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return false;

        try
        {
            return await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserName == userName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error checking username existence: {userName}", ex);
        }
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            return await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == email);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error checking email existence: {email}", ex);
        }
    }

    public async Task<IEnumerable<UserEntity>> GetActiveUsersAsync()
    {
        try
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.UserName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error retrieving active users", ex);
        }
    }
}
