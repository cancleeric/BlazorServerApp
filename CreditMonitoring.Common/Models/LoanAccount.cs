namespace CreditMonitoring.Common.Models;

public class LoanAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public string IdNumber { get; set; }  // 身份證號
    public decimal LoanAmount { get; set; }
    public DateTime LoanDate { get; set; }
    public DateTime MaturityDate { get; set; }  // 到期日
    public decimal InterestRate { get; set; }  // 利率
    public LoanStatus Status { get; set; }
    public int CreditScore { get; set; }
    public string Purpose { get; set; }  // 貸款用途

    // 導航屬性
    public List<CreditAlert> CreditAlerts { get; set; } = new();
    public List<Guarantor> Guarantors { get; set; } = new();
    public List<Collateral> Collaterals { get; set; } = new();
    public List<PaymentRecord> PaymentRecords { get; set; } = new();
    public List<LoanReview> LoanReviews { get; set; } = new();
}

public enum LoanStatus
{
    Applied,        // 已申請
    UnderReview,    // 審核中
    Approved,       // 已核准
    Rejected,       // 已拒絕
    Active,         // 生效中
    Closed,         // 已結案
    Default         // 違約
}