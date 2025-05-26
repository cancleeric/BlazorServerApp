namespace CreditMonitoring.Common.Models;

public class Collateral
{
    public int Id { get; set; }
    public int LoanAccountId { get; set; }
    public string CollateralType { get; set; }  // 擔保品類型
    public string Description { get; set; }
    public decimal EstimatedValue { get; set; }  // 估值
    public decimal LoanToValue { get; set; }  // 貸放成數
    public string Location { get; set; }  // 擔保品位置
    public string DocumentNumber { get; set; }  // 文件編號
    public DateTime ValuationDate { get; set; }
    public DateTime ExpiryDate { get; set; }  // 擔保期限

    // 導航屬性
    public LoanAccount LoanAccount { get; set; }
}