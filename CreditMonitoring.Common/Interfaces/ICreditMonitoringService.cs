using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Common.Interfaces
{
    public interface ICreditMonitoringService
    {
        Task<List<LoanAccount>> GetLoanAccountsAsync();
        Task<LoanAccount?> GetLoanAccountAsync(string accountNumber);
        Task<List<CreditAlert>> GetCreditAlertsAsync();
        Task<List<CreditAlert>> GetCreditAlertsForAccountAsync(string accountNumber);
        Task<List<LoanAccount>> GetDeterioratedCasesAsync();
        Task<bool> UpdateLoanAccountAsync(LoanAccount account);
        Task<bool> CreateCreditAlertAsync(CreditAlert alert);
        Task<bool> UpdateCreditAlertAsync(CreditAlert alert);
        
        // Additional methods for Web project
        Task<IEnumerable<LoanAccount>> GetAllAccountsAsync();
        Task<LoanAccount> GetAccountByIdAsync(int id);
        Task<IEnumerable<CreditAlert>> GetAccountAlertsAsync(int accountId);
        Task StartMonitoringAsync(int accountId);
    }
}
