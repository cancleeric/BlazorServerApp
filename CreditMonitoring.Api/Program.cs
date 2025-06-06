using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CreditMonitoring.Api.Data;
using CreditMonitoring.Api.Data.Interfaces;
using CreditMonitoring.Api.Data.Repositories;
using CreditMonitoring.Api.Services;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Common.Services;
using CreditMonitoring.Common.Services.Azure;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// === Azure 整合配置 ===
// 1. 設定 Azure Key Vault（如果在 Azure 環境中）
if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
{
    var keyVaultUri = builder.Configuration["Azure:KeyVault:VaultUri"];
    if (!string.IsNullOrEmpty(keyVaultUri))
    {
        builder.Configuration.AddAzureKeyVault(keyVaultUri, reloadOnChange: true);
    }
}

// 2. 添加 Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// 3. 註冊 Azure 服務
builder.Services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
builder.Services.AddSingleton<IAzureServiceBusService, AzureServiceBusService>();
builder.Services.AddSingleton<IAzureMonitoringService, AzureMonitoringService>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT Configuration
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "YourVerySecureSecretKeyThatIsAtLeast32CharactersLong123456789";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CreditMonitoring.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CreditMonitoring.Web";

// 註冊JWT相關服務
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// 配置JWT身份認證
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 配置授權
builder.Services.AddAuthorization(options =>
{
    // 定義角色基礎的授權策略
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireCreditOfficer", policy => policy.RequireRole("CreditOfficer", "Manager", "Admin"));
    options.AddPolicy("RequireManager", policy => policy.RequireRole("Manager", "Admin"));
    options.AddPolicy("RequireAuditor", policy => policy.RequireRole("Auditor", "Admin"));
});

// 配置CORS以支援Web應用程式
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins("http://localhost:5003", "https://localhost:5003")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddDbContext<CreditMonitoringDbContext>(options =>
{
    // 根據環境選擇資料庫
    if (builder.Environment.IsDevelopment())
    {
        // 開發環境使用 In-Memory 資料庫
        options.UseInMemoryDatabase("CreditMonitoringDb");
    }
    else
    {
        // 生產環境使用 Azure SQL Database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }
});

builder.Services.AddScoped<ILoanAccountRepository, LoanAccountRepository>();
builder.Services.AddScoped<ILoanAccountService, LoanAccountService>();

var app = builder.Build();

// === Azure 初始化 ===
// 確保資料庫已建立（僅在非開發環境）
if (!app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CreditMonitoringDbContext>();
        dbContext.Database.EnsureCreated();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 使用CORS
app.UseCors("AllowWebApp");

// 使用身份認證和授權中間件
app.UseAuthentication();
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