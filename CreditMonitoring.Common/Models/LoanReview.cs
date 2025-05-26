namespace CreditMonitoring.Common.Models;

public class LoanReview
{
    public int Id { get; set; }
    public int LoanAccountId { get; set; }
    public DateTime ReviewDate { get; set; }
    public string ReviewType { get; set; }  // 審查類型
    public string Reviewer { get; set; }  // 審查人
    public string Result { get; set; }  // 審查結果
    public string Comments { get; set; }  // 審查意見
    public int PreviousCreditScore { get; set; }
    public int UpdatedCreditScore { get; set; }
    public ReviewStatus Status { get; set; }

    // 導航屬性
    public LoanAccount LoanAccount { get; set; }
}

public enum ReviewStatus
{
    Pending,    // 待審查
    InProgress, // 審查中
    Completed,  // 已完成
    Rejected    // 已拒絕
}