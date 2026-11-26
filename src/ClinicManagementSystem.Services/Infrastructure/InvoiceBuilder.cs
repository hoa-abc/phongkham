using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.DataAccess.Entities.Clinical;

namespace ClinicManagementSystem.Services.Infrastructure;

/// <summary>
/// Lập (hoặc lập lại) hóa đơn CHƯA THANH TOÁN cho một lượt khám từ:
///   dịch vụ được chỉ định  +  thuốc trong đơn.
/// Được gọi bên trong transaction của ConsultationService, nên "khám xong" và "có hóa đơn" luôn đi cùng nhau.
/// Yêu cầu: visit đã được nạp kèm ServiceOrders.Service, Prescription.Items.Medicine và Invoices.Lines.
/// </summary>
internal static class InvoiceBuilder
{
    public static Invoice Upsert(Visit visit, int createdByUserId, DateTime nowUtc)
    {
        var lines = new List<InvoiceLine>();

        foreach (var order in visit.ServiceOrders)
        {
            lines.Add(new InvoiceLine
            {
                Description = order.Service.Name,
                Quantity = order.Quantity,
                UnitLabel = "lần",
                UnitPrice = order.UnitPriceSnapshot,
                LineTotal = order.UnitPriceSnapshot * order.Quantity
            });
        }

        if (visit.Prescription is { Status: not PrescriptionStatus.Cancelled } prescription)
        {
            foreach (var item in prescription.Items)
            {
                lines.Add(new InvoiceLine
                {
                    Description = $"Thuốc: {item.Medicine.Name} {item.Medicine.Strength}",
                    Quantity = item.Quantity,
                    UnitLabel = item.Medicine.Unit,
                    UnitPrice = item.Medicine.UnitPrice,
                    LineTotal = Math.Round(item.Medicine.UnitPrice * item.Quantity, 0, MidpointRounding.AwayFromZero)
                });
            }
        }

        var total = lines.Sum(x => x.LineTotal);

        // Hóa đơn chưa thanh toán thì chưa khóa -> được phép tính lại. Hóa đơn đã hủy thì bỏ qua và lập hóa đơn mới.
        var invoice = visit.Invoices.SingleOrDefault(x => x.Status == InvoiceStatus.Unpaid);
        if (invoice is null)
        {
            invoice = new Invoice
            {
                InvoiceNumber = CodeFormatter.InvoiceNumber(visit.VisitId, visit.Invoices.Count + 1),
                Status = InvoiceStatus.Unpaid,
                IsLocked = false,
                CreatedAtUtc = nowUtc,
                CreatedByUserId = createdByUserId
            };
            visit.Invoices.Add(invoice);
        }
        else
        {
            invoice.Lines.Clear(); // EF Core sẽ DELETE các dòng cũ (quan hệ Cascade)
        }

        foreach (var line in lines)
        {
            invoice.Lines.Add(line);
        }

        invoice.TotalAmount = total;
        return invoice;
    }
}
