using CreditMonitoring.Api.Data.Interfaces;
using CreditMonitoring.Common.Interfaces;
using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Api.Services;

public class LoanAccountService : ILoanAccountService
{
    private readonly ILoanAccountRepository _repository;
    private readonly ILogger<LoanAccountService> _logger;

    public LoanAccountService(
        ILoanAccountRepository repository,
        ILogger<LoanAccountService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<LoanAccount> GetAccountByIdAsync(int id)
    {
        var account = await _repository.GetByIdAsync(id);
        if (account == null)
        {
            throw new KeyNotFoundException($"貸款帳戶 ID {id} 不存在");
        }
        return account;
    }

    public async Task<IEnumerable<LoanAccount>> GetAllAccountsAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<IEnumerable<CreditAlert>> GetAccountAlertsAsync(int accountId)
    {
        return await _repository.GetAlertsByAccountIdAsync(accountId);
    }

    public async Task<CreditAlert> CreateAlertAsync(int accountId, CreditAlert alert)
    {
        var account = await GetAccountByIdAsync(accountId);

        alert.LoanAccountId = accountId;
        alert.AlertDate = DateTime.UtcNow;

        var createdAlert = await _repository.AddAlertAsync(alert);
        _logger.LogInformation($"為帳戶 {accountId} 創建了新的信用警報");

        return createdAlert;
    }

    public async Task StartMonitoringAsync(int accountId)
    {
        var account = await GetAccountByIdAsync(accountId);
        // TODO: 實現開始監控的邏輯
        _logger.LogInformation($"開始監控帳戶 {accountId}");
    }
}