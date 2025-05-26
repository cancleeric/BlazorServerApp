using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Api.Data.Interfaces;

public interface ILoanAccountRepository
{
    Task<LoanAccount> GetByIdAsync(int id);
    Task<IEnumerable<LoanAccount>> GetAllAsync();
    Task<IEnumerable<CreditAlert>> GetAlertsByAccountIdAsync(int accountId);
    Task<CreditAlert> AddAlertAsync(CreditAlert alert);
    Task UpdateAccountAsync(LoanAccount account);
}