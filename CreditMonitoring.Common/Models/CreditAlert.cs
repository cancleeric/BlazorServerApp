namespace CreditMonitoring.Common.Models;

public class CreditAlert
{
    public int Id { get; set; }
    public int LoanAccountId { get; set; }
    public int AccountId => LoanAccountId; // SignalR 服務需要的屬性
    public int? VoucherId { get; set; }  // 關聯的問題傳票
    public DateTime AlertDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Message => Description; // SignalR 服務需要的屬性
    public int PreviousCreditScore { get; set; }
    public int CurrentCreditScore { get; set; }
    public AlertSeverity Severity { get; set; }

    // 導航屬性
    public LoanAccount LoanAccount { get; set; } = null!;
    public Voucher? Voucher { get; set; }
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}