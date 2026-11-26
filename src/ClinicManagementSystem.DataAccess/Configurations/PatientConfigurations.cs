using ClinicManagementSystem.DataAccess.Entities.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagementSystem.DataAccess.Configurations;

internal sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> b)
    {
        b.ToTable("Guardians");
        b.HasKey(x => x.GuardianId);
        b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        // Số điện thoại là "chìa khóa tra cứu" phụ huynh nên phải duy nhất (nhưng KHÔNG dùng làm khóa chính).
        b.HasIndex(x => x.PhoneNumber).IsUnique();
    }
}

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("Patients");
        b.HasKey(x => x.PatientId);
        b.Property(x => x.MedicalRecordNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        b.Property(x => x.DateOfBirth).HasColumnType("date");
        b.Property(x => x.Gender).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.GuardianRelationship).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.HasIndex(x => x.MedicalRecordNumber).IsUnique();
        // Restrict: không cho xóa phụ huynh khi còn hồ sơ con -> không có bệnh nhi "mồ côi" dữ liệu.
        b.HasOne(x => x.Guardian).WithMany(x => x.Patients)
            .HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AllergyConfiguration : IEntityTypeConfiguration<Allergy>
{
    public void Configure(EntityTypeBuilder<Allergy> b)
    {
        b.ToTable("Allergies");
        b.HasKey(x => x.AllergyId);
        b.Property(x => x.AllergenKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        b.Property(x => x.Reaction).HasMaxLength(500);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.RecordedAtUtc).HasColumnType("datetime2");
        // Filtered unique index: một bệnh nhi không có hai dị ứng ĐANG HIỆU LỰC trùng khóa.
        b.HasIndex(x => new { x.PatientId, x.AllergenKey }).IsUnique()
            .HasDatabaseName("UX_Allergy_ActivePatientAllergen")
            .HasFilter("[IsActive] = 1");
        b.HasOne(x => x.Patient).WithMany(x => x.Allergies)
            .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}
