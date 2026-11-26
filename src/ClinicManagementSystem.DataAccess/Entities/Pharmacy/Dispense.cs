using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

/// <summary>Phiếu cấp phát. Chỉ được tạo khi cấp phát THÀNH CÔNG; mỗi đơn thuốc tối đa một phiếu.</summary>
public sealed class Dispense
{
    public int DispenseId { get; set; }
    public int PrescriptionId { get; set; }
    public int ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public DispenseStatus Status { get; set; } = DispenseStatus.Confirmed;

    public ICollection<DispenseItem> Items { get; set; } = new List<DispenseItem>();
}
