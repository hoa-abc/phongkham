using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using ClinicManagementSystem.DataAccess.Entities.Patients;

namespace ClinicManagementSystem.DataAccess.Entities.Appointments;

public sealed class Appointment
{
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int BookedByUserId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string? Reason { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>
    /// Concurrency token: SQL Server tự đổi giá trị này mỗi lần dòng được cập nhật.
    /// Khi lưu, EF Core thêm "WHERE RowVersion = giá trị lúc đọc"; nếu ai đó đã sửa trước -> báo xung đột.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    public Patient Patient { get; set; } = null!;
    public User Doctor { get; set; } = null!;
}
