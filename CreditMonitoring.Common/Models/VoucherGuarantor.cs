namespace CreditMonitoring.Common.Models;

public class VoucherGuarantor
{
    public int VoucherId { get; set; }
    public int GuarantorId { get; set; }
    public DateTime GuaranteeDate { get; set; }  // 擔保日期
    public decimal GuaranteeAmount { get; set; }  // 擔保金額
    public bool IsActive { get; set; }

    // 導航屬性
    public Voucher Voucher { get; set; }
    public Guarantor Guarantor { get; set; }
}