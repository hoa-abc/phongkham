using ClinicManagementSystem.DataAccess.Entities.Auditing;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagementSystem.DataAccess.Configurations;

// Fluent API: mô tả bảng bằng code C#. Mỗi lớp cấu hình cho một entity.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.Specialty).HasMaxLength(160);
        // Lưu enum dạng chuỗi ("Doctor") thay vì số (3): đọc bảng và nhật ký dễ hiểu hơn.
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.HasIndex(x => x.UserName).IsUnique();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.AuditLogId);
        b.Property(x => x.ActorNameSnapshot).HasMaxLength(160).IsRequired();
        b.Property(x => x.Action).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(80).IsRequired();
        b.Property(x => x.OccurredAtUtc).HasColumnType("datetime2");
        b.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.EntityName, x.EntityId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });
        // Xóa tài khoản thì nhật ký vẫn còn (ActorUserId thành NULL, tên vẫn lưu ở ActorNameSnapshot).
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
