using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Patients;

/// <summary>
/// Dị ứng đã ghi nhận của bệnh nhi. AllergenKey là khóa đã CHUẨN HÓA (viết hoa, bỏ khoảng trắng thừa),
/// dùng chung quy tắc với Medicine.ActiveIngredientKey / TherapeuticGroupKey để so khớp khi kê đơn.
/// Đây là dữ liệu cảnh báo, không thay thế đánh giá chuyên môn của bác sĩ.
/// </summary>
public sealed class Allergy
{
    public int AllergyId { get; set; }
    public int PatientId { get; set; }
    public string AllergenKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Reaction { get; set; }
    public AllergySeverity Severity { get; set; }
    /// <summary>Không xóa cứng dị ứng; chỉ đánh dấu ngừng hiệu lực để giữ lịch sử.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime RecordedAtUtc { get; set; }

    public Patient Patient { get; set; } = null!;
}
