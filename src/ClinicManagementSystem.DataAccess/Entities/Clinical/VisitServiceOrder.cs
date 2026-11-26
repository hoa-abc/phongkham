using ClinicManagementSystem.DataAccess.Entities.Billing;

namespace ClinicManagementSystem.DataAccess.Entities.Clinical;

/// <summary>Dịch vụ được chỉ định trong lượt khám (ví dụ: khám nhi tổng quát, tư vấn dinh dưỡng).</summary>
public sealed class VisitServiceOrder
{
    public int VisitServiceOrderId { get; set; }
    public int VisitId { get; set; }
    public int ServiceId { get; set; }
    public int Quantity { get; set; } = 1;
    /// <summary>"Chụp" giá tại thời điểm chỉ định: sau này bảng giá đổi cũng không làm sai hóa đơn cũ.</summary>
    public decimal UnitPriceSnapshot { get; set; }
    public int OrderedByUserId { get; set; }
    public DateTime OrderedAtUtc { get; set; }

    public ClinicService Service { get; set; } = null!;
}
