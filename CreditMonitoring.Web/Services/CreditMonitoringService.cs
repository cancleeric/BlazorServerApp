using System.Net.Http.Json;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Web.Models;

namespace CreditMonitoring.Web.Services;

public interface ICreditMonitoringService
{
    Task<IEnumerable<LoanAccount>> GetAllAccountsAsync();
    Task<LoanAccount> GetAccountByIdAsync(int id);
    Task<IEnumerable<CreditAlert>> GetAccountAlertsAsync(int accountId);
    Task StartMonitoringAsync(int accountId);
    Task<List<DeterioratedCaseViewModel>> GetDeterioratedCasesAsync();
    Task<CaseDetailViewModel> GetCaseDetailAsync(int loanAccountId);
}

public class CreditMonitoringService : ICreditMonitoringService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public CreditMonitoringService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri(_configuration["ApiSettings:BaseUrl"]);
    }

    public async Task<IEnumerable<LoanAccount>> GetAllAccountsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<LoanAccount>>("api/creditmonitoring/accounts")
            ?? Enumerable.Empty<LoanAccount>();
    }

    public async Task<LoanAccount> GetAccountByIdAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<LoanAccount>($"api/creditmonitoring/accounts/{id}")
            ?? throw new KeyNotFoundException($"找不到ID為 {id} 的貸款帳戶");
    }

    public async Task<IEnumerable<CreditAlert>> GetAccountAlertsAsync(int accountId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<CreditAlert>>($"api/creditmonitoring/accounts/{accountId}/alerts")
            ?? Enumerable.Empty<CreditAlert>();
    }

    public async Task StartMonitoringAsync(int accountId)
    {
        var response = await _httpClient.PostAsync($"api/creditmonitoring/accounts/{accountId}/monitor", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DeterioratedCaseViewModel>> GetDeterioratedCasesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DeterioratedCaseViewModel>>(
            "api/creditmonitoring/deteriorated-cases") ?? new List<DeterioratedCaseViewModel>();
    }

    public async Task<CaseDetailViewModel> GetCaseDetailAsync(int loanAccountId)
    {
        return await _httpClient.GetFromJsonAsync<CaseDetailViewModel>(
            $"api/creditmonitoring/case-details/{loanAccountId}")
            ?? throw new KeyNotFoundException($"找不到案件 ID {loanAccountId}");
    }
}