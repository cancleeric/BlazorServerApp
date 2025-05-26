using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Web.Models
{
    public class CaseDetailViewModel
    {
        public int LoanAccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal LoanAmount { get; set; }
        public int CurrentCreditScore { get; set; }
        public int ScoreChange { get; set; }
        public DateTime LastAlertDate { get; set; }
        public LoanAccount LoanAccount { get; set; } = new();  // 貸款帳戶詳細資訊
        public BankOfficer BankOfficer { get; set; } = new();  // 銀行行員資訊
        public List<CreditAlert> CreditAlerts { get; set; } = new();
        public List<PaymentRecord> PaymentHistory { get; set; } = new();
        public List<Guarantor> Guarantors { get; set; } = new();
        public List<Collateral> Collaterals { get; set; } = new();
        public List<LoanReview> Reviews { get; set; } = new();
        public List<Voucher> Vouchers { get; set; } = new();
    }
}
