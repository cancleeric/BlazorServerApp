using Microsoft.EntityFrameworkCore;
using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 授權碼 Repository 實作 - 遵循單一責任原則 (SRP)
/// 確保授權碼的單次使用特性和安全性
/// </summary>
public class AuthorizationCodeRepository : IAuthorizationCodeRepository
{
    private readonly LocalIdentityDbContext _context;

    public AuthorizationCodeRepository(LocalIdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AuthorizationCodeEntity> StoreAsync(AuthorizationCodeEntity authCode)
    {
        if (authCode == null)
            throw new ArgumentNullException(nameof(authCode));

        if (string.IsNullOrWhiteSpace(authCode.Code))
            throw new ArgumentException("Authorization code cannot be null or empty", nameof(authCode));

        try
        {
            _context.AuthorizationCodes.Add(authCode);
            await _context.SaveChangesAsync();
            return authCode;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error storing authorization code: {authCode.Code}", ex);
        }
    }

    public async Task<AuthorizationCodeEntity?> TakeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));

        try
        {
            // 使用交易確保原子性 - 防止競爭條件
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            var authCode = await _context.AuthorizationCodes
                .Include(ac => ac.Client)
                .Include(ac => ac.User)
                .FirstOrDefaultAsync(ac => ac.Code == code && !ac.IsUsed);

            if (authCode == null)
                return null;

            // 檢查是否過期
            if (authCode.ExpiresAt < DateTime.UtcNow)
            {
                // 標記為已使用以防止後續使用
                authCode.IsUsed = true;
                authCode.UsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return null;
            }

            // 標記為已使用 - 確保單次使用原則
            authCode.IsUsed = true;
            authCode.UsedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return authCode;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error taking authorization code: {code}", ex);
        }
    }

    public async Task<AuthorizationCodeEntity?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));

        try
        {
            return await _context.AuthorizationCodes
                .AsNoTracking()
                .Include(ac => ac.Client)
                .Include(ac => ac.User)
                .FirstOrDefaultAsync(ac => ac.Code == code);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving authorization code: {code}", ex);
        }
    }

    public async Task CleanupExpiredAsync()
    {
        try
        {
            var expiredCodes = await _context.AuthorizationCodes
                .Where(ac => ac.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredCodes.Any())
            {
                _context.AuthorizationCodes.RemoveRange(expiredCodes);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error cleaning up expired authorization codes", ex);
        }
    }

    public async Task RevokeByUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        try
        {
            var userCodes = await _context.AuthorizationCodes
                .Where(ac => ac.Subject == userId && !ac.IsUsed)
                .ToListAsync();

            foreach (var code in userCodes)
            {
                code.IsUsed = true;
                code.UsedAt = DateTime.UtcNow;
            }

            if (userCodes.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error revoking authorization codes for user: {userId}", ex);
        }
    }

    public async Task RevokeByClientAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            var clientCodes = await _context.AuthorizationCodes
                .Where(ac => ac.ClientId == clientId && !ac.IsUsed)
                .ToListAsync();

            foreach (var code in clientCodes)
            {
                code.IsUsed = true;
                code.UsedAt = DateTime.UtcNow;
            }

            if (clientCodes.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error revoking authorization codes for client: {clientId}", ex);
        }
    }
}
