using ClinicManagementSystem.DataAccess.Entities.Identity;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagementSystem.DataAccess.Configurations;

internal sealed class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> b)
    {
        // Tồn kho không bao giờ âm - database tự chặn.
        b.ToTable("Medicines", t =>
            t.HasCheckConstraint("CK_Medicine_NonNegativeStock", "[StockQuantity] >= 0"));
        b.HasKey(x => x.MedicineId);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(180).IsRequired();
        b.Property(x => x.ActiveIngredient).HasMaxLength(240).IsRequired();
        b.Property(x => x.ActiveIngredientKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.TherapeuticGroupKey).HasMaxLength(100);
        b.Property(x => x.Strength).HasMaxLength(80).IsRequired();
        b.Property(x => x.DosageForm).HasMaxLength(80).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.StockQuantity).HasPrecision(18, 3);
        b.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.ActiveIngredientKey);
    }
}

internal sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> b)
    {
        b.ToTable("Prescriptions");
        b.HasKey(x => x.PrescriptionId);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.IssuedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.ReturnReason).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.VisitId).IsUnique();
        b.HasOne(x => x.Visit).WithOne(x => x.Prescription).HasForeignKey<Prescription>(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReturnedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> b)
    {
        b.ToTable("PrescriptionItems", t =>
            t.HasCheckConstraint("CK_PrescriptionItem_PositiveQuantity", "[Quantity] > 0"));
        b.HasKey(x => x.PrescriptionItemId);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.DosageInstructions).HasMaxLength(1000);
        b.HasOne<Prescription>().WithMany(x => x.Items).HasForeignKey(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PrescriptionStatusHistoryConfiguration : IEntityTypeConfiguration<PrescriptionStatusHistory>
{
    public void Configure(EntityTypeBuilder<PrescriptionStatusHistory> b)
    {
        b.ToTable("PrescriptionStatusHistory");
        b.HasKey(x => x.PrescriptionStatusHistoryId);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasOne<Prescription>().WithMany(x => x.StatusHistory).HasForeignKey(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DispenseConfiguration : IEntityTypeConfiguration<Dispense>
{
    public void Configure(EntityTypeBuilder<Dispense> b)
    {
        b.ToTable("Dispenses");
        b.HasKey(x => x.DispenseId);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.ConfirmedAtUtc).HasColumnType("datetime2");
        // CHỐT CHẶN chống cấp phát hai lần: mỗi đơn thuốc chỉ có một phiếu cấp phát.
        b.HasIndex(x => x.PrescriptionId).IsUnique();
        b.HasOne<Prescription>().WithOne().HasForeignKey<Dispense>(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DispenseItemConfiguration : IEntityTypeConfiguration<DispenseItem>
{
    public void Configure(EntityTypeBuilder<DispenseItem> b)
    {
        b.ToTable("DispenseItems", t =>
            t.HasCheckConstraint("CK_DispenseItem_PositiveQuantity", "[Quantity] > 0"));
        b.HasKey(x => x.DispenseItemId);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.PrescriptionItemId).IsUnique();
        b.HasOne(x => x.Dispense).WithMany(x => x.Items).HasForeignKey(x => x.DispenseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PrescriptionItem>().WithMany().HasForeignKey(x => x.PrescriptionItemId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> b)
    {
        b.ToTable("InventoryTransactions");
        b.HasKey(x => x.InventoryTransactionId);
        b.Property(x => x.QuantityChange).HasPrecision(18, 3);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(300);
        b.Property(x => x.OccurredAtUtc).HasColumnType("datetime2");
        // Một phiếu cấp phát chỉ xuất kho mỗi thuốc đúng một lần.
        b.HasIndex(x => new { x.DispenseId, x.MedicineId }).IsUnique()
            .HasDatabaseName("UX_InventoryTransaction_DispenseMedicine")
            .HasFilter("[DispenseId] IS NOT NULL");
        b.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Dispense).WithMany().HasForeignKey(x => x.DispenseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
