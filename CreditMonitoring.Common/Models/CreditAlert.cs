namespace CreditMonitoring.Common.Models;

public class CreditAlert
{
    public int Id { get; set; }
    public int LoanAccountId { get; set; }
    public int? VoucherId { get; set; }  // 關聯的問題傳票
    public DateTime AlertDate { get; set; }
    public string AlertType { get; set; }
    public string Description { get; set; }
    public int PreviousCreditScore { get; set; }
    public int CurrentCreditScore { get; set; }
    public AlertSeverity Severity { get; set; }

    // 導航屬性
    public LoanAccount LoanAccount { get; set; }
    public Voucher Voucher { get; set; }
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}