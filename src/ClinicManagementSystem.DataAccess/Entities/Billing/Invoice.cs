using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.DataAccess.Entities.Clinical;

namespace ClinicManagementSystem.DataAccess.Entities.Billing;

public sealed class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int VisitId { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    /// <summary>Hóa đơn đã thanh toán thì bị khóa: không ai được sửa dòng hay tổng tiền nữa.</summary>
    public bool IsLocked { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public int CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Visit Visit { get; set; } = null!;
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
