namespace CreditMonitoring.Common.Models;

public class Guarantor
{
    public int Id { get; set; }
    public int LoanAccountId { get; set; }
    public string Name { get; set; }
    public string IdNumber { get; set; }
    public string ContactNumber { get; set; }
    public string Address { get; set; }
    public string Relationship { get; set; }  // 與借款人關係
    public int CreditScore { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }

    // 導航屬性
    public LoanAccount LoanAccount { get; set; }
    public List<VoucherGuarantor> VoucherGuarantors { get; set; } = new();
}