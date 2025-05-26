using CreditMonitoring.Common.Interfaces;
using CreditMonitoring.Web.Models;

namespace CreditMonitoring.Web.Interfaces
{
    public interface IWebCreditMonitoringService : ICreditMonitoringService
    {
        Task<List<DeterioratedCaseViewModel>> GetDeterioratedCasesViewModelAsync();
        Task<CaseDetailViewModel> GetCaseDetailAsync(int loanAccountId);
    }
}
