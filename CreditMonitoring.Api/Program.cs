using Microsoft.EntityFrameworkCore;
using CreditMonitoring.Api.Data;
using CreditMonitoring.Api.Data.Interfaces;
using CreditMonitoring.Api.Data.Repositories;
using CreditMonitoring.Api.Services;
using CreditMonitoring.Common.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CreditMonitoringDbContext>(options =>
    options.UseInMemoryDatabase("CreditMonitoringDb"));

builder.Services.AddScoped<ILoanAccountRepository, LoanAccountRepository>();
builder.Services.AddScoped<ILoanAccountService, LoanAccountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// 添加測試數據
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CreditMonitoringDbContext>();
    SeedTestData(context);
}

app.Run();

// 測試數據種子方法
void SeedTestData(CreditMonitoringDbContext context)
{
    if (!context.LoanAccounts.Any())
    {
        // 創建貸款帳戶
        var loanAccount1 = new LoanAccount
        {
            AccountNumber = "LA20240001",
            CustomerName = "張三",
            IdNumber = "A123456789",
            LoanAmount = 1000000M,
            LoanDate = DateTime.Now.AddMonths(-6),
            MaturityDate = DateTime.Now.AddYears(2),
            InterestRate = 3.5M,
            Status = LoanStatus.Active,
            CreditScore = 750,
            Purpose = "創業資金"
        };

        var loanAccount2 = new LoanAccount
        {
            AccountNumber = "LA20240002",
            CustomerName = "李四",
            IdNumber = "B123456789",
            LoanAmount = 2000000M,
            LoanDate = DateTime.Now.AddMonths(-3),
            MaturityDate = DateTime.Now.AddYears(3),
            InterestRate = 4.0M,
            Status = LoanStatus.Active,
            CreditScore = 680,
            Purpose = "購置設備"
        };

        context.LoanAccounts.AddRange(loanAccount1, loanAccount2);
        context.SaveChanges();

        // 添加擔保人
        var guarantor1 = new Guarantor
        {
            LoanAccountId = loanAccount1.Id,
            Name = "王五",
            IdNumber = "C123456789",
            ContactNumber = "0912345678",
            Address = "台北市信義區信義路100號",
            Relationship = "父子",
            GuaranteeAmount = 1000000M,
            CreditScore = 780,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        context.Guarantors.Add(guarantor1);

        // 添加擔保品
        var collateral1 = new Collateral
        {
            LoanAccountId = loanAccount1.Id,
            CollateralType = "不動產",
            Description = "台北市大安區住宅",
            EstimatedValue = 5000000M,
            LoanToValue = 0.6M,
            Location = "台北市大安區和平東路50號",
            DocumentNumber = "COL20240001",
            ValuationDate = DateTime.Now,
            ExpiryDate = DateTime.Now.AddYears(3)
        };

        context.Collaterals.Add(collateral1);

        // 添加還款記錄
        var payment1 = new PaymentRecord
        {
            LoanAccountId = loanAccount1.Id,
            DueDate = DateTime.Now.AddMonths(-1),
            PaymentDate = DateTime.Now.AddMonths(-1).AddDays(5),
            Amount = 30000M,
            PrincipalAmount = 25000M,
            InterestAmount = 5000M,
            Status = PaymentStatus.Paid,
            Notes = "準時還款"
        };

        context.PaymentRecords.Add(payment1);

        // 添加審查記錄
        var review1 = new LoanReview
        {
            LoanAccountId = loanAccount1.Id,
            ReviewDate = DateTime.Now.AddMonths(-1),
            ReviewType = "定期審查",
            Reviewer = "陳經理",
            Result = "通過",
            Comments = "營運狀況良好",
            PreviousCreditScore = 730,
            UpdatedCreditScore = 750,
            Status = ReviewStatus.Completed
        };

        context.LoanReviews.Add(review1);

        // 添加信用警報
        var alert1 = new CreditAlert
        {
            LoanAccountId = loanAccount2.Id,
            AlertDate = DateTime.Now.AddDays(-15),
            AlertType = "信用分數下降",
            Description = "信用卡逾期還款",
            PreviousCreditScore = 720,
            CurrentCreditScore = 680,
            Severity = AlertSeverity.Medium
        };

        context.CreditAlerts.Add(alert1);

        // 添加傳票
        var voucher1 = new Voucher
        {
            LoanAccountId = loanAccount1.Id,
            VoucherNumber = "V20240001",
            IssueDate = DateTime.Now.AddMonths(-3),
            Amount = 500000M,
            Status = VoucherStatus.Normal,
            Description = "第一期放款"
        };

        var voucher2 = new Voucher
        {
            LoanAccountId = loanAccount1.Id,
            VoucherNumber = "V20240002",
            IssueDate = DateTime.Now.AddMonths(-2),
            Amount = 500000M,
            Status = VoucherStatus.Overdue,
            Description = "第二期放款"
        };

        context.Vouchers.AddRange(voucher1, voucher2);
        context.SaveChanges();

        // 添加傳票擔保人關聯
        var voucherGuarantor1 = new VoucherGuarantor
        {
            VoucherId = voucher1.Id,
            GuarantorId = guarantor1.Id,
            GuaranteeDate = DateTime.Now.AddMonths(-3),
            GuaranteeAmount = 500000M,
            IsActive = true
        };

        var voucherGuarantor2 = new VoucherGuarantor
        {
            VoucherId = voucher2.Id,
            GuarantorId = guarantor1.Id,
            GuaranteeDate = DateTime.Now.AddMonths(-2),
            GuaranteeAmount = 500000M,
            IsActive = true
        };

        context.VoucherGuarantors.AddRange(voucherGuarantor1, voucherGuarantor2);

        // 更新信用警報，關聯到問題傳票
        alert1.VoucherId = voucher2.Id;

        context.SaveChanges();
    }
}