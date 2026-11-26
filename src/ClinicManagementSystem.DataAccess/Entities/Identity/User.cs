using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Identity;

// Entity = lớp C# ánh xạ 1-1 với một bảng trong SQL Server. Mỗi property là một cột.
public sealed class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Chuỗi băm PBKDF2, không phải mật khẩu gốc.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    /// <summary>Chuyên khoa/phòng khám, chỉ có ý nghĩa với bác sĩ.</summary>
    public string? Specialty { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
