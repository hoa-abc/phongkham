using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;

namespace ClinicManagementSystem.Desktop.ViewModels.Reception;

// "Row" = một dòng trong bảng/danh sách. Nó bọc DTO và chuẩn bị sẵn chữ để hiển thị,
// nhờ vậy XAML chỉ việc {Binding PatientLine} mà không phải tự ghép chuỗi hay đổi giờ UTC.

/// <summary>Một dòng lịch hẹn: luôn có mã bệnh nhi + phụ huynh + bác sĩ + trạng thái (yêu cầu của phần C).</summary>
public sealed class AppointmentRow(AppointmentListItemDto dto)
{
    public AppointmentListItemDto Dto { get; } = dto;
    public int AppointmentId => Dto.AppointmentId;
    public string Time => Display.Time(Dto.StartsAtUtc);
    public string Date => Display.Date(Dto.StartsAtUtc);
    public string PatientName => Dto.PatientName;
    public string MedicalRecordNumber => Dto.MedicalRecordNumber;
    public string GuardianLine => $"PH: {Dto.GuardianName} · {Display.Phone(Dto.GuardianPhone)}";
    public string DoctorName => Dto.DoctorName;
    public string DoctorSpecialty => Dto.DoctorSpecialty ?? string.Empty;
    public AppointmentStatus Status => Dto.Status;
    public string StatusLabel => Dto.Status.ToLabel();
    public string? Reason => Dto.Reason;

    /// <summary>Máy trạng thái: chỉ lịch "Đã đặt" mới check-in hoặc hủy được.</summary>
    public bool CanAct => Dto.Status == AppointmentStatus.Booked;
}

/// <summary>Một hồ sơ bệnh nhi trong danh sách chọn / tìm kiếm.</summary>
public sealed class PatientRow(PatientListItemDto dto)
{
    public PatientListItemDto Dto { get; } = dto;
    public int PatientId => Dto.PatientId;
    public string MedicalRecordNumber => Dto.MedicalRecordNumber;
    public string FullName => Dto.FullName;
    public string Age => Display.Age(Dto.DateOfBirth);
    public string GuardianLine => $"{Dto.GuardianName} · {Display.Phone(Dto.GuardianPhone)}";
    public bool HasAllergies => Dto.Allergies.Count > 0;
    public string AllergyText => HasAllergies ? "⚠ Dị ứng: " + string.Join(", ", Dto.Allergies) : "Không ghi nhận dị ứng";

    /// <summary>Chữ hiện trong ô chọn: mã trước, tên sau - để phân biệt hai bé trùng tên.</summary>
    public string Label => $"{MedicalRecordNumber} · {FullName} · PH {Dto.GuardianName}";

    public override string ToString() => Label;
}

/// <summary>Một khung giờ khám 30 phút trong lưới chọn giờ.</summary>
public sealed class SlotItem(SlotDto dto)
{
    public TimeOnly Start { get; } = TimeOnly.FromDateTime(dto.StartLocal);
    public string Label => Start.ToString("HH:mm");
    public bool IsAvailable { get; } = dto.IsAvailable;

    /// <summary>"Còn trống", "Đã đặt" hoặc "Đã qua giờ" - luôn có chữ, không chỉ dựa vào màu.</summary>
    public string Note { get; } = dto.UnavailableReason ?? "Còn trống";
}
