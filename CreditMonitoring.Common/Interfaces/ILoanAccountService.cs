using CreditMonitoring.Common.Models;

public interface ILoanAccountService
{
    Task<LoanAccount> GetAccountByIdAsync(int id);
    Task<IEnumerable<LoanAccount>> GetAllAccountsAsync();
    Task<IEnumerable<CreditAlert>> GetAccountAlertsAsync(int accountId);
    Task<CreditAlert> CreateAlertAsync(int accountId, CreditAlert alert);
    Task StartMonitoringAsync(int accountId);
}