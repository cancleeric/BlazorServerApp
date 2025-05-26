using System.Net.Http.Json;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Common.Interfaces;
using CreditMonitoring.Web.Models;
using CreditMonitoring.Web.Interfaces;

namespace CreditMonitoring.Web.Services;

public class CreditMonitoringService : IWebCreditMonitoringService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public CreditMonitoringService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri(_configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001");
    }

    // ICreditMonitoringService implementations
    public async Task<List<LoanAccount>> GetLoanAccountsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LoanAccount>>("api/creditmonitoring/accounts")
            ?? new List<LoanAccount>();
    }

    public async Task<LoanAccount?> GetLoanAccountAsync(string accountNumber)
    {
        return await _httpClient.GetFromJsonAsync<LoanAccount>($"api/creditmonitoring/accounts/by-number/{accountNumber}");
    }

    public async Task<List<CreditAlert>> GetCreditAlertsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CreditAlert>>("api/creditmonitoring/alerts")
            ?? new List<CreditAlert>();
    }

    public async Task<List<CreditAlert>> GetCreditAlertsForAccountAsync(string accountNumber)
    {
        return await _httpClient.GetFromJsonAsync<List<CreditAlert>>($"api/creditmonitoring/accounts/{accountNumber}/alerts")
            ?? new List<CreditAlert>();
    }

    public async Task<List<LoanAccount>> GetDeterioratedCasesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LoanAccount>>("api/creditmonitoring/deteriorated-cases")
            ?? new List<LoanAccount>();
    }

    public async Task<bool> UpdateLoanAccountAsync(LoanAccount account)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/creditmonitoring/accounts/{account.Id}", account);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CreateCreditAlertAsync(CreditAlert alert)
    {
        var response = await _httpClient.PostAsJsonAsync("api/creditmonitoring/alerts", alert);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateCreditAlertAsync(CreditAlert alert)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/creditmonitoring/alerts/{alert.Id}", alert);
        return response.IsSuccessStatusCode;
    }

    // Additional methods for Web project
    public async Task<IEnumerable<LoanAccount>> GetAllAccountsAsync()
    {
        return await GetLoanAccountsAsync();
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

    // ViewModel-specific methods
    public async Task<List<DeterioratedCaseViewModel>> GetDeterioratedCasesViewModelAsync()
    {        // 暫時返回模擬數據，實際應該從 API 獲取並轉換
        var accounts = await GetDeterioratedCasesAsync();
        return accounts.Select(account => new DeterioratedCaseViewModel
        {
            LoanAccountId = account.Id,
            AccountNumber = account.AccountNumber,
            CustomerName = account.CustomerName,
            IdNumber = account.IdNumber,
            LoanAmount = account.LoanAmount,
            CurrentBalance = account.LoanAmount * 0.8m, // 模擬數據
            CurrentCreditScore = account.CreditScore,
            ScoreChange = -50, // 模擬數據
            LastAlertDate = DateTime.Now.AddDays(-1),
            LastUpdateDate = DateTime.Now.AddDays(-1),
            AlertCount = 3,
            OverdueDays = 15, // 模擬數據
            VoucherNumbers = "V001, V002",
            HighestSeverity = AlertSeverity.High,
            RiskLevel = "高風險", // 模擬數據
            AlertSeverity = AlertSeverity.High // 模擬數據
        }).ToList();
    }

    public async Task<CaseDetailViewModel> GetCaseDetailAsync(int loanAccountId)
    {
        // 暫時返回模擬數據，實際應該從 API 獲取
        try
        {
            var account = await GetAccountByIdAsync(loanAccountId);
            
            return new CaseDetailViewModel
            {
                LoanAccountId = account.Id,
                AccountNumber = account.AccountNumber,
                CustomerName = account.CustomerName,
                LoanAmount = account.LoanAmount,
                CurrentCreditScore = account.CreditScore,
                ScoreChange = -50, // 模擬數據
                LastAlertDate = DateTime.Now.AddDays(-1),
                LoanAccount = account,
                BankOfficer = new BankOfficer
                {
                    Id = 1,
                    OfficerId = "EMP001",
                    Name = "張經理",
                    Department = "信貸部",
                    ContactNumber = "02-1234-5678",
                    Email = "manager.chang@bank.com",
                    Title = "經理"
                },
                CreditAlerts = account.CreditAlerts ?? new List<CreditAlert>(),
                PaymentHistory = account.PaymentRecords ?? new List<PaymentRecord>(),
                Guarantors = account.Guarantors ?? new List<Guarantor>(),
                Collaterals = account.Collaterals ?? new List<Collateral>(),
                Reviews = account.LoanReviews ?? new List<LoanReview>(),
                Vouchers = new List<Voucher>() // 模擬空的傳票列表
            };
        }
        catch
        {
            // 如果 API 調用失敗，返回基本的模擬數據
            return new CaseDetailViewModel
            {
                LoanAccountId = loanAccountId,
                AccountNumber = "ACC001",
                CustomerName = "測試客戶",
                LoanAmount = 1000000,
                CurrentCreditScore = 650,
                ScoreChange = -50,
                LastAlertDate = DateTime.Now.AddDays(-1),
                LoanAccount = new LoanAccount
                {
                    Id = loanAccountId,
                    AccountNumber = "ACC001",
                    CustomerName = "測試客戶",
                    LoanAmount = 1000000,
                    CreditScore = 650
                },
                BankOfficer = new BankOfficer
                {
                    Id = 1,
                    Name = "張經理",
                    Department = "信貸部",
                    ContactNumber = "02-1234-5678",
                    Email = "manager.chang@bank.com"
                }
            };
        }
    }
}