using Microsoft.AspNetCore.Mvc;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Common.Interfaces;

namespace CreditMonitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CreditMonitoringController : ControllerBase
{
    private readonly ILoanAccountService _loanAccountService;
    private readonly ILogger<CreditMonitoringController> _logger;

    public CreditMonitoringController(
        ILoanAccountService loanAccountService,
        ILogger<CreditMonitoringController> logger)
    {
        _loanAccountService = loanAccountService;
        _logger = logger;
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<IEnumerable<LoanAccount>>> GetAccounts()
    {
        var accounts = await _loanAccountService.GetAllAccountsAsync();
        return Ok(accounts);
    }

    [HttpGet("accounts/{id}")]
    public async Task<ActionResult<LoanAccount>> GetAccount(int id)
    {
        try
        {
            var account = await _loanAccountService.GetAccountByIdAsync(id);
            return Ok(account);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("accounts/{id}/alerts")]
    public async Task<ActionResult<IEnumerable<CreditAlert>>> GetCreditAlerts(int id)
    {
        var alerts = await _loanAccountService.GetAccountAlertsAsync(id);
        return Ok(alerts);
    }

    [HttpGet("deteriorated-cases")]
    public async Task<ActionResult<IEnumerable<LoanAccount>>> GetDeterioratedCases()
    {
        var accounts = await _loanAccountService.GetAllAccountsAsync();
        // 篩選出有信用警報或狀態不良的案例
        var deterioratedCases = accounts.Where(a => 
            a.CreditAlerts.Any() || 
            a.Status == LoanStatus.Default ||
            a.CreditScore < 600
        );
        return Ok(deterioratedCases);
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<CreditAlert>>> GetAllAlerts()
    {
        var accounts = await _loanAccountService.GetAllAccountsAsync();
        var allAlerts = accounts.SelectMany(a => a.CreditAlerts);
        return Ok(allAlerts);
    }

    [HttpPost("alerts")]
    public async Task<ActionResult> CreateAlert([FromBody] CreditAlert alert)
    {
        // 這裡應該實現創建警報的邏輯
        // 暫時返回成功狀態
        return Ok();
    }

    [HttpPut("accounts/{id}")]
    public async Task<ActionResult> UpdateAccount(int id, [FromBody] LoanAccount account)
    {
        try
        {
            if (id != account.Id)
            {
                return BadRequest("ID 不匹配");
            }
            
            // 這裡應該實現更新邏輯
            // 暫時返回成功狀態
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("accounts/by-number/{accountNumber}")]
    public async Task<ActionResult<LoanAccount>> GetAccountByNumber(string accountNumber)
    {
        var accounts = await _loanAccountService.GetAllAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.AccountNumber == accountNumber);
        
        if (account == null)
        {
            return NotFound($"找不到帳戶號碼為 {accountNumber} 的貸款帳戶");
        }
        
        return Ok(account);
    }

    [HttpPost("accounts/{id}/monitor")]
    public async Task<ActionResult> StartMonitoring(int id)
    {
        await _loanAccountService.StartMonitoringAsync(id);
        return Ok();
    }
}