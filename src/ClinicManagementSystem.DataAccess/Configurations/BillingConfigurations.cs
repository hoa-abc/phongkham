using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagementSystem.DataAccess.Configurations;

internal sealed class ClinicServiceConfiguration : IEntityTypeConfiguration<ClinicService>
{
    public void Configure(EntityTypeBuilder<ClinicService> b)
    {
        b.ToTable("ServiceCatalog", t =>
            t.HasCheckConstraint("CK_ServiceCatalog_NonNegativePrice", "[Price] >= 0"));
        b.HasKey(x => x.ServiceId);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(180).IsRequired();
        // Tiền LUÔN dùng decimal (không dùng float/double vì sai số làm tròn).
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        // CHECK constraint: database tự bảo đảm "đã Paid thì phải Locked", dù code có lỗi cũng không lọt.
        b.ToTable("Invoices", t =>
            t.HasCheckConstraint("CK_Invoice_PaidMustBeLocked", "[Status] <> N'Paid' OR [IsLocked] = 1"));
        b.HasKey(x => x.InvoiceId);
        b.Property(x => x.InvoiceNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.PaidAtUtc).HasColumnType("datetime2");
        b.Property(x => x.VoidedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.InvoiceNumber).IsUnique();
        // Mỗi lượt khám chỉ có MỘT hóa đơn còn hiệu lực (hóa đơn đã hủy không tính).
        b.HasIndex(x => x.VisitId).IsUnique()
            .HasDatabaseName("UX_Invoice_OneActiveInvoicePerVisit")
            .HasFilter("[Status] <> N'Voided'");
        b.HasOne(x => x.Visit).WithMany(x => x.Invoices).HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("InvoiceLines");
        b.HasKey(x => x.InvoiceLineId);
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.UnitLabel).HasMaxLength(40);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.HasOne<Invoice>().WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", t =>
            t.HasCheckConstraint("CK_Payment_PositiveAmount", "[Amount] > 0"));
        b.HasKey(x => x.PaymentId);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.PaidAtUtc).HasColumnType("datetime2");
        // Một hóa đơn chỉ có MỘT lần thanh toán thành công -> bấm "thu tiền" hai lần không thu hai lần.
        b.HasIndex(x => x.InvoiceId).IsUnique()
            .HasDatabaseName("UX_Payment_OneSuccessfulPaymentPerInvoice")
            .HasFilter("[Status] = N'Succeeded'");
        b.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
