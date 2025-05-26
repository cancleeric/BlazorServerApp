using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Web.Models;

public class DeterioratedCaseViewModel
{
    public int LoanAccountId { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public string IdNumber { get; set; }  // 身分證字號
    public decimal LoanAmount { get; set; }
    public decimal CurrentBalance { get; set; }  // 現有餘額
    public int CurrentCreditScore { get; set; }
    public int ScoreChange { get; set; }
    public DateTime LastAlertDate { get; set; }
    public DateTime LastUpdateDate { get; set; }  // 最後更新日期
    public int AlertCount { get; set; }
    public int OverdueDays { get; set; }  // 逾期天數
    public string VoucherNumbers { get; set; }  // 問題傳票號碼（可能多筆）
    public AlertSeverity HighestSeverity { get; set; }
    public string RiskLevel { get; set; } = string.Empty;  // 風險等級
    public AlertSeverity AlertSeverity { get; set; }  // 警報嚴重程度
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