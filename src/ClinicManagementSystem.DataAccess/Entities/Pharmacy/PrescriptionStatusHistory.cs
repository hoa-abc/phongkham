using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

/// <summary>Mỗi lần đơn thuốc đổi trạng thái đều ghi một dòng: ai đổi, lúc nào, vì sao.</summary>
public sealed class PrescriptionStatusHistory
{
    public int PrescriptionStatusHistoryId { get; set; }
    public int PrescriptionId { get; set; }
    public PrescriptionStatus FromStatus { get; set; }
    public PrescriptionStatus ToStatus { get; set; }
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? Reason { get; set; }
}
