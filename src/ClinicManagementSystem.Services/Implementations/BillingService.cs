using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class BillingService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IBillingService
{
    public async Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(InvoiceStatus? status, CancellationToken ct = default)
    {
        guard.Require("xem danh sách hóa đơn", UserRole.Admin, UserRole.Receptionist);

        var query = db.Invoices.AsNoTracking();
        if (status is { } value)
        {
            query = query.Where(x => x.Status == value);
        }

        return await ProjectToListItem(query.OrderByDescending(x => x.CreatedAtUtc).Take(200)).ToListAsync(ct);
    }

    public async Task<InvoiceDetailDto?> GetAsync(int invoiceId, CancellationToken ct = default)
    {
        guard.Require("xem hóa đơn", UserRole.Admin, UserRole.Receptionist);

        var invoice = await db.Invoices.AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .Include(x => x.Visit).ThenInclude(x => x.Appointment).ThenInclude(x => x.Doctor)
            .Include(x => x.Visit).ThenInclude(x => x.Prescription)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return null;
        }

        var patient = await PatientContextLoader.LoadAsync(db, invoice.Visit.Appointment.PatientId, ct);
        if (patient is null)
        {
            return null;
        }

        var prescriptionStatus = invoice.Visit.Prescription?.Status;
        var cannotPayReason = GetCannotPayReason(invoice, prescriptionStatus);
        var paymentMethod = invoice.Payments
            .OrderByDescending(x => x.PaidAtUtc)
            .Select(x => (PaymentMethod?)x.Method)
            .FirstOrDefault();

        return new InvoiceDetailDto(
            invoice.InvoiceId, invoice.InvoiceNumber, invoice.Status, invoice.IsLocked, invoice.TotalAmount,
            invoice.CreatedAtUtc, invoice.PaidAtUtc, paymentMethod, invoice.VoidReason, patient,
            invoice.Visit.Appointment.Doctor.DisplayName,
            invoice.Lines.OrderBy(x => x.InvoiceLineId)
                .Select(x => new InvoiceLineDto(x.Description, x.Quantity, x.UnitLabel, x.UnitPrice, x.LineTotal)).ToList(),
            cannotPayReason is null,
            cannotPayReason,
            CanVoid(invoice, prescriptionStatus));
    }

    /// <summary>
    /// Thu tiền TOÀN PHẦN. Trong một transaction: hóa đơn -> Paid + khóa, tạo Payment,
    /// đơn thuốc Issued -> ReadyForDispense, lượt khám sang bước kế tiếp, ghi nhật ký.
    /// </summary>
    public async Task<ServiceResult<PaymentDto>> ProcessPaymentAsync(
        int invoiceId, PaymentMethod method, CancellationToken ct = default)
    {
        guard.Require("xác nhận thanh toán hóa đơn", UserRole.Admin, UserRole.Receptionist);

        if (invoiceId <= 0 || !Enum.IsDefined(method))
        {
            return ServiceResult<PaymentDto>.Failure(
                "PAYMENT_INPUT_INVALID", "Mã hóa đơn hoặc phương thức thanh toán không hợp lệ.");
        }

        var invoice = await db.Invoices
            .Include(x => x.Visit).ThenInclude(x => x.Prescription)
            .SingleOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return ServiceResult<PaymentDto>.Failure("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
        }

        var prescription = invoice.Visit.Prescription;
        var cannotPayReason = GetCannotPayReason(invoice, prescription?.Status);
        if (cannotPayReason is not null)
        {
            return ServiceResult<PaymentDto>.Failure("INVOICE_NOT_PAYABLE", cannotPayReason);
        }

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId!.Value;
        var before = new { invoice.Status, invoice.IsLocked };

        invoice.Status = InvoiceStatus.Paid;
        invoice.IsLocked = true; // từ đây không ai sửa được hóa đơn này nữa
        invoice.PaidAtUtc = now;

        var payment = new Payment
        {
            Invoice = invoice,
            Amount = invoice.TotalAmount,
            Method = method,
            Status = PaymentStatus.Succeeded,
            PaidAtUtc = now,
            ReceivedByUserId = userId
        };
        db.Payments.Add(payment);

        var hasMedicineToDispense = false;
        if (prescription is { Status: PrescriptionStatus.Issued })
        {
            prescription.Status = PrescriptionStatus.ReadyForDispense;
            db.PrescriptionStatusHistory.Add(new PrescriptionStatusHistory
            {
                PrescriptionId = prescription.PrescriptionId,
                FromStatus = PrescriptionStatus.Issued,
                ToStatus = PrescriptionStatus.ReadyForDispense,
                ChangedByUserId = userId,
                ChangedAtUtc = now,
                Reason = "Hóa đơn đã thanh toán."
            });
            hasMedicineToDispense = true;
        }

        // Có thuốc thì sang quầy dược; không có thuốc thì lượt khám kết thúc tại đây.
        invoice.Visit.Status = hasMedicineToDispense ? VisitStatus.AwaitingDispense : VisitStatus.Completed;
        invoice.Visit.CompletedAtUtc = hasMedicineToDispense ? null : now;

        AuditLogWriter.Add(db, currentUser, "Invoice.PaymentConfirmed", nameof(Invoice), invoice.InvoiceId, before,
            new { invoice.Status, invoice.IsLocked, PaymentAmount = payment.Amount, PaymentMethod = payment.Method });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion của hóa đơn đã đổi: quầy khác vừa xử lý hóa đơn này.
            return ServiceResult<PaymentDto>.Failure(
                "INVOICE_CHANGED", "Hóa đơn vừa được xử lý bởi người khác. Hãy tải lại trước khi thao tác.");
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            // Unique index "một thanh toán thành công / hóa đơn" đã chặn lần thu thứ hai.
            return ServiceResult<PaymentDto>.Failure(
                "INVOICE_ALREADY_PAID", "Hóa đơn vừa được thanh toán. Không ghi nhận thanh toán lần thứ hai.");
        }

        return ServiceResult<PaymentDto>.Success(
            new PaymentDto(payment.PaymentId, payment.InvoiceId, payment.Amount, payment.Method, payment.Status, payment.PaidAtUtc),
            hasMedicineToDispense
                ? "Thanh toán thành công. Hóa đơn đã khóa và đơn thuốc đã chuyển sang quầy dược."
                : "Thanh toán thành công. Hóa đơn đã khóa và lượt khám hoàn tất.");
    }

    /// <summary>
    /// Nghiệp vụ ĐIỀU CHỈNH: hủy hóa đơn đã thu và hoàn tiền khi dược sĩ trả đơn về bác sĩ.
    /// Không sửa trực tiếp hóa đơn đã khóa; thay vào đó đánh dấu Voided (kèm lý do, người làm, thời điểm)
    /// để bác sĩ sửa đơn và hệ thống lập hóa đơn MỚI.
    /// </summary>
    public async Task<ServiceResult> VoidAsync(int invoiceId, string? reason, CancellationToken ct = default)
    {
        guard.Require("hủy hóa đơn và hoàn tiền", UserRole.Admin, UserRole.Receptionist);

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrEmpty(normalizedReason) || normalizedReason.Length > 500)
        {
            return ServiceResult.Failure("VOID_REASON_REQUIRED", "Vui lòng nhập lý do hủy hóa đơn (tối đa 500 ký tự).");
        }

        var invoice = await db.Invoices
            .Include(x => x.Payments)
            .Include(x => x.Visit).ThenInclude(x => x.Prescription)
            .SingleOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return ServiceResult.Failure("INVOICE_NOT_FOUND", "Không tìm thấy hóa đơn.");
        }

        if (!CanVoid(invoice, invoice.Visit.Prescription?.Status))
        {
            return ServiceResult.Failure("INVOICE_NOT_VOIDABLE",
                "Chỉ hủy được hóa đơn đã thanh toán khi đơn thuốc của lượt khám đang bị dược sĩ trả về bác sĩ.");
        }

        var now = DateTime.UtcNow;
        var before = new { invoice.Status };
        invoice.Status = InvoiceStatus.Voided; // vẫn giữ IsLocked = true: hóa đơn hủy cũng không được sửa
        invoice.VoidedAtUtc = now;
        invoice.VoidReason = normalizedReason;
        foreach (var payment in invoice.Payments.Where(x => x.Status == PaymentStatus.Succeeded))
        {
            payment.Status = PaymentStatus.Reversed;
        }

        AuditLogWriter.Add(db, currentUser, "Invoice.Voided", nameof(Invoice), invoice.InvoiceId, before,
            new { invoice.Status, invoice.VoidReason, RefundedAmount = invoice.TotalAmount });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Failure("INVOICE_CHANGED", "Hóa đơn vừa được xử lý bởi người khác. Hãy tải lại.");
        }

        return ServiceResult.Success("Đã hủy hóa đơn và ghi nhận hoàn tiền. Bác sĩ có thể chỉnh sửa đơn thuốc.");
    }

    // ------------------------------------------------------------------ QUY TẮC DÙNG CHUNG

    /// <summary>Trả về null nếu thu tiền được; ngược lại trả về lý do để giao diện giải thích cho nhân viên.</summary>
    private static string? GetCannotPayReason(Invoice invoice, PrescriptionStatus? prescriptionStatus)
    {
        if (invoice.Status == InvoiceStatus.Paid)
        {
            return "Hóa đơn đã thanh toán và đang bị khóa.";
        }

        if (invoice.Status == InvoiceStatus.Voided)
        {
            return "Hóa đơn đã bị hủy.";
        }

        if (invoice.IsLocked)
        {
            return "Hóa đơn đang bị khóa.";
        }

        if (invoice.TotalAmount <= 0)
        {
            return "Tổng tiền hóa đơn phải lớn hơn 0.";
        }

        if (prescriptionStatus is PrescriptionStatus.Draft or PrescriptionStatus.ReturnedToDoctor)
        {
            return "Đơn thuốc đang được bác sĩ chỉnh sửa. Chờ bác sĩ phát hành lại đơn rồi mới thu tiền.";
        }

        return null;
    }

    private static bool CanVoid(Invoice invoice, PrescriptionStatus? prescriptionStatus)
        => invoice.Status == InvoiceStatus.Paid && prescriptionStatus == PrescriptionStatus.ReturnedToDoctor;

    internal static IQueryable<InvoiceListItemDto> ProjectToListItem(IQueryable<Invoice> query)
        => query.Select(x => new InvoiceListItemDto(
            x.InvoiceId, x.InvoiceNumber,
            x.Visit.Appointment.Patient.FullName, x.Visit.Appointment.Patient.MedicalRecordNumber,
            x.TotalAmount, x.Status, x.CreatedAtUtc, x.PaidAtUtc,
            x.Status == InvoiceStatus.Paid && x.Visit.Prescription != null
                && x.Visit.Prescription.Status == PrescriptionStatus.ReturnedToDoctor));
}
