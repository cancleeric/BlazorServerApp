namespace CreditMonitoring.Common.Models;

public class PaymentRecord
{
    public int Id { get; set; }
    public int LoanAccountId { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PrincipalAmount { get; set; }  // 本金
    public decimal InterestAmount { get; set; }  // 利息
    public PaymentStatus Status { get; set; }
    public string? Notes { get; set; }

    // 導航屬性
    public LoanAccount LoanAccount { get; set; }
}

public enum PaymentStatus
{
    Pending,    // 待繳款
    Paid,       // 已繳款
    Overdue,    // 逾期
    Defaulted   // 違約
}