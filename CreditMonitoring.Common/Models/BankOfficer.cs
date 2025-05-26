namespace CreditMonitoring.Common.Models;

public class BankOfficer
{
    public int Id { get; set; }
    public string OfficerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;  // 職稱
    public DateTime JoinDate { get; set; }

    // 導航屬性
    public List<LoanAccount> ManagedAccounts { get; set; } = new();
}
