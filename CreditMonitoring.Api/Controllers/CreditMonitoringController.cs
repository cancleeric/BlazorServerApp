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

    [HttpPost("accounts/{id}/monitor")]
    public async Task<ActionResult> StartMonitoring(int id)
    {
        await _loanAccountService.StartMonitoringAsync(id);
        return Ok();
    }
}