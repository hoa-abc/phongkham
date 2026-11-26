using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.DataAccess.Entities.Appointments;
using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;

namespace ClinicManagementSystem.DataAccess.Entities.Clinical;

/// <summary>
/// Lượt khám: được tạo khi tiếp tân check-in một lịch hẹn (quan hệ 1-1 với Appointment).
/// Đây là "xương sống" nối sinh hiệu, chẩn đoán, chỉ định dịch vụ, đơn thuốc và hóa đơn.
/// </summary>
public sealed class Visit
{
    public int VisitId { get; set; }
    public int AppointmentId { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Waiting;
    public string? ChiefComplaint { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentPlan { get; set; }
    public DateTime CheckedInAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Appointment Appointment { get; set; } = null!;
    public ICollection<VitalSign> VitalSigns { get; set; } = new List<VitalSign>();
    public ICollection<VisitServiceOrder> ServiceOrders { get; set; } = new List<VisitServiceOrder>();
    /// <summary>Mỗi lượt khám có tối đa MỘT đơn thuốc.</summary>
    public Prescription? Prescription { get; set; }
    /// <summary>Thường chỉ một hóa đơn; có thêm khi hóa đơn cũ bị hủy (Voided) và lập lại.</summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
