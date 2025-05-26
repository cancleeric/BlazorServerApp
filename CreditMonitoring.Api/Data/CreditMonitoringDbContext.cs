using Microsoft.EntityFrameworkCore;
using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Api.Data;

public class CreditMonitoringDbContext : DbContext
{
    public CreditMonitoringDbContext(DbContextOptions<CreditMonitoringDbContext> options)
        : base(options)
    {
        // 確保數據庫已創建
        Database.EnsureCreated();
    }

    public DbSet<LoanAccount> LoanAccounts { get; set; }
    public DbSet<CreditAlert> CreditAlerts { get; set; }
    public DbSet<Guarantor> Guarantors { get; set; }
    public DbSet<Collateral> Collaterals { get; set; }
    public DbSet<PaymentRecord> PaymentRecords { get; set; }
    public DbSet<LoanReview> LoanReviews { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<VoucherGuarantor> VoucherGuarantors { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // LoanAccount 配置
        modelBuilder.Entity<LoanAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IdNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.LoanAmount).HasPrecision(18, 2);
            entity.Property(e => e.InterestRate).HasPrecision(5, 2);
            entity.Property(e => e.Purpose).HasMaxLength(500);
        });

        // Guarantor 配置
        modelBuilder.Entity<Guarantor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IdNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.Relationship).HasMaxLength(50);
            entity.Property(e => e.GuaranteeAmount).HasPrecision(18, 2);

            entity.HasOne(e => e.LoanAccount)
                  .WithMany(e => e.Guarantors)
                  .HasForeignKey(e => e.LoanAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Collateral 配置
        modelBuilder.Entity<Collateral>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CollateralType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EstimatedValue).HasPrecision(18, 2);
            entity.Property(e => e.LoanToValue).HasPrecision(5, 2);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.DocumentNumber).HasMaxLength(50);

            entity.HasOne(e => e.LoanAccount)
                  .WithMany(e => e.Collaterals)
                  .HasForeignKey(e => e.LoanAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // PaymentRecord 配置
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PrincipalAmount).HasPrecision(18, 2);
            entity.Property(e => e.InterestAmount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.LoanAccount)
                  .WithMany(e => e.PaymentRecords)
                  .HasForeignKey(e => e.LoanAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // LoanReview 配置
        modelBuilder.Entity<LoanReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReviewType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reviewer).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Result).HasMaxLength(50);
            entity.Property(e => e.Comments).HasMaxLength(1000);

            entity.HasOne(e => e.LoanAccount)
                  .WithMany(e => e.LoanReviews)
                  .HasForeignKey(e => e.LoanAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Voucher 配置
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VoucherNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.LoanAccount)
                  .WithMany()
                  .HasForeignKey(e => e.LoanAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // VoucherGuarantor 配置
        modelBuilder.Entity<VoucherGuarantor>(entity =>
        {
            entity.HasKey(e => new { e.VoucherId, e.GuarantorId });
            entity.Property(e => e.GuaranteeAmount).HasPrecision(18, 2);

            entity.HasOne(e => e.Voucher)
                  .WithMany(e => e.VoucherGuarantors)
                  .HasForeignKey(e => e.VoucherId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Guarantor)
                  .WithMany(e => e.VoucherGuarantors)
                  .HasForeignKey(e => e.GuarantorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // CreditAlert 配置
        modelBuilder.Entity<CreditAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AlertType).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne<LoanAccount>()
                  .WithMany(e => e.CreditAlerts)
                  .HasForeignKey(e => e.LoanAccountId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Voucher)
                  .WithMany(e => e.CreditAlerts)
                  .HasForeignKey(e => e.VoucherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}