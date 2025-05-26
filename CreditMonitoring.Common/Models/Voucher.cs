namespace CreditMonitoring.Common.Models;

public class Voucher
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; }  // 傳票號碼
    public int LoanAccountId { get; set; }
    public DateTime IssueDate { get; set; }  // 發行日期
    public decimal Amount { get; set; }  // 金額
    public VoucherStatus Status { get; set; }
    public string Description { get; set; }

    // 導航屬性
    public LoanAccount LoanAccount { get; set; }
    public List<VoucherGuarantor> VoucherGuarantors { get; set; } = new();
    public List<CreditAlert> CreditAlerts { get; set; } = new();
}

public enum VoucherStatus
{
    Normal,     // 正常
    Overdue,    // 逾期
    Defaulted,  // 違約
    Settled     // 已結清
}