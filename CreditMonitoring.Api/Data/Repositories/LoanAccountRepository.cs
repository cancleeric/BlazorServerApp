using CreditMonitoring.Api.Data.Interfaces;
using CreditMonitoring.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditMonitoring.Api.Data.Repositories;

public class LoanAccountRepository : ILoanAccountRepository
{
    private readonly CreditMonitoringDbContext _context;

    public LoanAccountRepository(CreditMonitoringDbContext context)
    {
        _context = context;
    }

    public async Task<LoanAccount> GetByIdAsync(int id)
    {
        return await _context.LoanAccounts
            .Include(la => la.CreditAlerts)
            .FirstOrDefaultAsync(la => la.Id == id);
    }

    public async Task<IEnumerable<LoanAccount>> GetAllAsync()
    {
        return await _context.LoanAccounts
            .Include(la => la.CreditAlerts)
            .ToListAsync();
    }

    public async Task<IEnumerable<CreditAlert>> GetAlertsByAccountIdAsync(int accountId)
    {
        return await _context.CreditAlerts
            .Where(ca => ca.LoanAccountId == accountId)
            .OrderByDescending(ca => ca.AlertDate)
            .ToListAsync();
    }

    public async Task<CreditAlert> AddAlertAsync(CreditAlert alert)
    {
        _context.CreditAlerts.Add(alert);
        await _context.SaveChangesAsync();
        return alert;
    }

    public async Task UpdateAccountAsync(LoanAccount account)
    {
        _context.Entry(account).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }
}