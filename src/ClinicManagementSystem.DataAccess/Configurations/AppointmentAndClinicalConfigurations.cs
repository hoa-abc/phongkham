using ClinicManagementSystem.DataAccess.Entities.Appointments;
using ClinicManagementSystem.DataAccess.Entities.Clinical;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagementSystem.DataAccess.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments", t =>
            t.HasCheckConstraint("CK_Appointment_ValidTime", "[EndsAtUtc] > [StartsAtUtc]"));
        b.HasKey(x => x.AppointmentId);
        b.Property(x => x.StartsAtUtc).HasColumnType("datetime2");
        b.Property(x => x.EndsAtUtc).HasColumnType("datetime2");
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.CancelledAtUtc).HasColumnType("datetime2");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();

        // CHỐT CHẶN CUỐI chống trùng lịch: database từ chối hai lịch còn hiệu lực
        // có cùng bác sĩ + cùng giờ bắt đầu, kể cả khi hai request đến cùng một mili-giây.
        // Lịch đã hủy bị loại khỏi index (filter) nên khung giờ đó đặt lại được.
        b.HasIndex(x => new { x.DoctorId, x.StartsAtUtc })
            .IsUnique()
            .HasDatabaseName("UX_Appointment_ActiveDoctorStart")
            .HasFilter("[Status] <> N'Cancelled'");
        b.HasIndex(x => x.StartsAtUtc);

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.BookedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> b)
    {
        b.ToTable("Visits");
        b.HasKey(x => x.VisitId);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.ChiefComplaint).HasMaxLength(1000);
        b.Property(x => x.ClinicalNotes).HasMaxLength(4000);
        b.Property(x => x.Diagnosis).HasMaxLength(1000);
        b.Property(x => x.TreatmentPlan).HasMaxLength(2000);
        b.Property(x => x.CheckedInAtUtc).HasColumnType("datetime2");
        b.Property(x => x.StartedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.RowVersion).IsRowVersion();
        // Unique: một lịch hẹn chỉ sinh ra đúng một lượt khám -> bấm check-in hai lần không tạo hai lượt.
        b.HasIndex(x => x.AppointmentId).IsUnique();
        b.HasOne(x => x.Appointment).WithOne().HasForeignKey<Visit>(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VitalSignConfiguration : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> b)
    {
        b.ToTable("VitalSigns");
        b.HasKey(x => x.VitalSignId);
        b.Property(x => x.WeightKg).HasPrecision(5, 2);
        b.Property(x => x.HeightCm).HasPrecision(5, 1);
        b.Property(x => x.TemperatureC).HasPrecision(4, 1);
        b.Property(x => x.RecordedAtUtc).HasColumnType("datetime2");
        b.HasOne<Visit>().WithMany(x => x.VitalSigns).HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VisitServiceOrderConfiguration : IEntityTypeConfiguration<VisitServiceOrder>
{
    public void Configure(EntityTypeBuilder<VisitServiceOrder> b)
    {
        b.ToTable("VisitServiceOrders", t =>
            t.HasCheckConstraint("CK_VisitServiceOrder_PositiveQuantity", "[Quantity] > 0"));
        b.HasKey(x => x.VisitServiceOrderId);
        b.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
        b.Property(x => x.OrderedAtUtc).HasColumnType("datetime2");
        b.HasIndex(x => new { x.VisitId, x.ServiceId }).IsUnique();
        b.HasOne<Visit>().WithMany(x => x.ServiceOrders).HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OrderedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
