using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 客戶端 Repository 實作 - 遵循 SOLID 原則
/// </summary>
public class ClientRepository : IClientRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<ClientRepository> _logger;

    public ClientRepository(LocalIdentityDbContext context, ILogger<ClientRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClientEntity?> GetByClientIdAsync(string clientId)
    {
        try
        {
            _logger.LogDebug("Searching for client: {ClientId}", clientId);

            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);

            if (client != null)
            {
                _logger.LogDebug("Client found: {ClientId}", clientId);
            }
            else
            {
                _logger.LogDebug("Client not found: {ClientId}", clientId);
            }

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<ClientEntity> AddAsync(ClientEntity client)
    {
        try
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _logger.LogInformation("Creating new client: {ClientId}", client.ClientId);

            // 檢查是否已存在
            var existingClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.ClientId == client.ClientId);

            if (existingClient != null)
            {
                throw new InvalidOperationException($"Client with ID '{client.ClientId}' already exists");
            }

            // 設定建立時間
            client.CreatedAt = DateTime.UtcNow;
            client.IsActive = true;

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Client created successfully: {ClientId}", client.ClientId);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client: {ClientId}", client?.ClientId);
            throw;
        }
    }

    public async Task UpdateAsync(ClientEntity client)
    {
        try
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _logger.LogInformation("Updating client: {ClientId}", client.ClientId);

            var existingClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.ClientId == client.ClientId);

            if (existingClient == null)
            {
                throw new InvalidOperationException($"Client with ID '{client.ClientId}' not found");
            }

            // 更新可變更的欄位
            existingClient.ClientName = client.ClientName;
            existingClient.ClientSecret = client.ClientSecret;
            existingClient.RedirectUris = client.RedirectUris;
            existingClient.AllowedScopes = client.AllowedScopes;
            existingClient.RequireConsent = client.RequireConsent;
            existingClient.ClientType = client.ClientType;
            existingClient.IsActive = client.IsActive;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Client updated successfully: {ClientId}", client.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client: {ClientId}", client?.ClientId);
            throw;
        }
    }

    public async Task<bool> ExistsByClientIdAsync(string clientId)
    {
        try
        {
            _logger.LogDebug("Checking if client exists: {ClientId}", clientId);

            var exists = await _context.Clients
                .AsNoTracking()
                .AnyAsync(c => c.ClientId == clientId);

            _logger.LogDebug("Client exists check result for {ClientId}: {Exists}", clientId, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking client existence: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<IEnumerable<ClientEntity>> GetActiveClientsAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving all active clients");

            var clients = await _context.Clients
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.ClientName)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} active clients", clients.Count);
            return clients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active clients");
            throw;
        }
    }

    public async Task<bool> ValidateClientCredentialsAsync(string clientId, string clientSecret)
    {
        try
        {
            _logger.LogDebug("Validating client credentials for: {ClientId}", clientId);

            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);

            if (client == null)
            {
                _logger.LogWarning("Client not found or inactive: {ClientId}", clientId);
                return false;
            }

            // 假設 ClientSecret 已經是雜湊值，實際應該使用 BCrypt 比較
            var isValid = BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret);

            if (isValid)
            {
                _logger.LogInformation("Client credentials validated successfully: {ClientId}", clientId);
            }
            else
            {
                _logger.LogWarning("Invalid client credentials: {ClientId}", clientId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client credentials: {ClientId}", clientId);
            return false;
        }
    }
}
