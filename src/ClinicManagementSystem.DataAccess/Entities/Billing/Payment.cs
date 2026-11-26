using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Billing;

public sealed class Payment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Succeeded;
    public DateTime PaidAtUtc { get; set; }
    public int ReceivedByUserId { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
