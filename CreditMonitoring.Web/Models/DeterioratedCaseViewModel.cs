namespace CreditMonitoring.Web.Models;

public class DeterioratedCaseViewModel
{
    public int LoanAccountId { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public decimal LoanAmount { get; set; }
    public int CurrentCreditScore { get; set; }
    public int ScoreChange { get; set; }
    public DateTime LastAlertDate { get; set; }
    public int AlertCount { get; set; }
    public string VoucherNumbers { get; set; }  // 問題傳票號碼（可能多筆）
    public AlertSeverity HighestSeverity { get; set; }
}

public class CaseDetailViewModel
{
    public LoanAccount LoanAccount { get; set; }
    public BankOfficerInfo BankOfficer { get; set; }
    public List<CreditAlert> CreditAlerts { get; set; }
    public List<GuarantorDetailInfo> Guarantors { get; set; }
    public List<VoucherInfo> ProblemVouchers { get; set; }
}

public class BankOfficerInfo
{
    public string OfficerId { get; set; }
    public string Name { get; set; }
    public string Department { get; set; }
    public string ContactNumber { get; set; }
    public string Email { get; set; }
}

public class GuarantorDetailInfo
{
    public Guarantor Guarantor { get; set; }
    public decimal TotalGuaranteeAmount { get; set; }
    public List<VoucherInfo> GuaranteedVouchers { get; set; }
}

public class VoucherInfo
{
    public Voucher Voucher { get; set; }
    public List<GuarantorInfo> Guarantors { get; set; }
    public List<CreditAlert> RelatedAlerts { get; set; }
}

public class GuarantorInfo
{
    public int GuarantorId { get; set; }
    public string Name { get; set; }
    public decimal GuaranteeAmount { get; set; }
}