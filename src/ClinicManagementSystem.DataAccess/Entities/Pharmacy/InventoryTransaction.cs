using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

/// <summary>
/// Sổ kho: tồn kho không bao giờ "tự nhiên" thay đổi - mỗi thay đổi đều có một dòng ở đây.
/// QuantityChange âm = xuất (cấp phát), dương = nhập/hoàn.
/// </summary>
public sealed class InventoryTransaction
{
    public int InventoryTransactionId { get; set; }
    public int MedicineId { get; set; }
    public int? DispenseId { get; set; }
    public decimal QuantityChange { get; set; }
    public InventoryTransactionType Type { get; set; }
    public string? Reference { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int PerformedByUserId { get; set; }

    public Medicine Medicine { get; set; } = null!;
    public Dispense? Dispense { get; set; }
}
