using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

/// <summary>
/// Nghiệp vụ của DƯỢC SĨ. Dược sĩ chỉ có hai lựa chọn với một đơn: CẤP PHÁT hoặc TRẢ VỀ bác sĩ kèm lý do.
/// Không có hàm nào cho dược sĩ sửa thuốc/số lượng trong đơn.
/// </summary>
public sealed class PharmacyService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IPharmacyService
{
    public Task<IReadOnlyList<PharmacyQueueItemDto>> GetQueueAsync(CancellationToken ct = default)
    {
        guard.Require("xem đơn chờ cấp phát", UserRole.Pharmacist);
        // Đơn "Chờ thanh toán" (Issued) cũng hiện để dược sĩ rà soát sớm và trả đơn TRƯỚC khi phụ huynh trả tiền.
        return QueryQueueAsync(x => x.Status == PrescriptionStatus.ReadyForDispense || x.Status == PrescriptionStatus.Issued,
            newestFirst: false, ct);
    }

    public Task<IReadOnlyList<PharmacyQueueItemDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        guard.Require("xem lịch sử cấp phát", UserRole.Pharmacist, UserRole.Admin);
        return QueryQueueAsync(x => x.Status == PrescriptionStatus.Dispensed || x.Status == PrescriptionStatus.ReturnedToDoctor,
            newestFirst: true, ct);
    }

    public async Task<PharmacyPrescriptionDto?> GetAsync(int prescriptionId, CancellationToken ct = default)
    {
        guard.Require("xem đơn thuốc", UserRole.Pharmacist, UserRole.Admin);

        var prescription = await db.Prescriptions.AsNoTracking()
            .Include(x => x.Items).ThenInclude(x => x.Medicine)
            .Include(x => x.Visit).ThenInclude(x => x.Appointment).ThenInclude(x => x.Doctor)
            .Include(x => x.Visit).ThenInclude(x => x.Invoices)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.PrescriptionId == prescriptionId, ct);
        if (prescription is null)
        {
            return null;
        }

        var patient = await PatientContextLoader.LoadAsync(db, prescription.Visit.Appointment.PatientId, ct);
        if (patient is null)
        {
            return null;
        }

        var history = await (
                from h in db.PrescriptionStatusHistory.AsNoTracking()
                join u in db.Users.AsNoTracking() on h.ChangedByUserId equals u.UserId
                where h.PrescriptionId == prescriptionId
                orderby h.ChangedAtUtc descending
                select new PrescriptionHistoryDto(h.ChangedAtUtc, h.FromStatus, h.ToStatus, u.DisplayName, h.Reason))
            .ToListAsync(ct);

        var invoice = prescription.Visit.Invoices.FirstOrDefault(x => x.Status != InvoiceStatus.Voided);
        var blockReason = GetDispenseBlockReason(prescription, invoice?.Status, invoice?.IsLocked ?? false);

        return new PharmacyPrescriptionDto(
            prescription.PrescriptionId, prescription.Status, patient,
            prescription.Visit.Appointment.Doctor.DisplayName, prescription.Visit.Diagnosis,
            prescription.Items.Select(x => new PharmacyItemDto(
                x.Medicine.Name, x.Medicine.Strength, x.Medicine.Unit, x.Quantity, x.Medicine.StockQuantity,
                x.DosageInstructions, x.Medicine.StockQuantity >= x.Quantity)).ToList(),
            invoice?.Status,
            blockReason is null,
            blockReason,
            CanReturn(prescription.Status),
            prescription.ReturnReason,
            history);
    }

    public async Task<ServiceResult<PrescriptionDto>> ReturnToDoctorAsync(
        int prescriptionId, string? reason, CancellationToken ct = default)
    {
        guard.Require("trả đơn thuốc về bác sĩ", UserRole.Pharmacist);

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrEmpty(normalizedReason))
        {
            return ServiceResult<PrescriptionDto>.Failure("RETURN_REASON_REQUIRED", "Vui lòng nhập lý do trả đơn thuốc.");
        }

        if (normalizedReason.Length > 1000)
        {
            return ServiceResult<PrescriptionDto>.Failure("RETURN_REASON_TOO_LONG", "Lý do trả đơn không được vượt quá 1.000 ký tự.");
        }

        var prescription = await db.Prescriptions
            .Include(x => x.Visit).ThenInclude(x => x.Appointment)
            .SingleOrDefaultAsync(x => x.PrescriptionId == prescriptionId, ct);
        if (prescription is null)
        {
            return ServiceResult<PrescriptionDto>.Failure("PRESCRIPTION_NOT_FOUND", "Không tìm thấy đơn thuốc.");
        }

        if (!CanReturn(prescription.Status))
        {
            return ServiceResult<PrescriptionDto>.Failure("PRESCRIPTION_STATE_INVALID",
                "Chỉ đơn đã được bác sĩ phát hành và chưa cấp phát mới được trả về.");
        }

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId!.Value;
        var oldStatus = prescription.Status;
        prescription.Status = PrescriptionStatus.ReturnedToDoctor;
        prescription.ReturnReason = normalizedReason;
        prescription.ReturnedByUserId = userId;
        prescription.StatusHistory.Add(new PrescriptionStatusHistory
        {
            FromStatus = oldStatus,
            ToStatus = PrescriptionStatus.ReturnedToDoctor,
            ChangedByUserId = userId,
            ChangedAtUtc = now,
            Reason = normalizedReason
        });

        // Lượt khám quay lại hàng đợi của bác sĩ để bác sĩ xử lý đơn.
        prescription.Visit.Status = VisitStatus.InConsultation;

        AuditLogWriter.Add(db, currentUser, "Prescription.ReturnedToDoctor", nameof(Prescription),
            prescription.PrescriptionId, new { Status = oldStatus },
            new { prescription.Status, ReturnReason = normalizedReason });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<PrescriptionDto>.Failure(
                "PRESCRIPTION_CHANGED", "Đơn thuốc vừa được cập nhật bởi người khác. Hãy tải lại danh sách.");
        }

        return ServiceResult<PrescriptionDto>.Success(
            new PrescriptionDto(prescription.PrescriptionId, prescription.Visit.Appointment.PatientId,
                prescription.Status, prescription.ReturnReason),
            "Đơn thuốc đã được chuyển về hàng đợi của bác sĩ.");
    }

    /// <summary>
    /// CẤP PHÁT NGUYÊN TỬ (atomic) và KHÔNG LẶP (idempotent). Một lần SaveChanges = một transaction gồm:
    ///   tạo phiếu cấp phát + trừ tồn từng thuốc + ghi sổ kho + đổi trạng thái đơn và lượt khám + nhật ký.
    /// Ba lớp bảo vệ khi hai dược sĩ (hoặc hai lần bấm) xử lý cùng một đơn:
    ///   (1) kiểm tra trạng thái đơn, (2) RowVersion trên Prescription và Medicine,
    ///   (3) unique index Dispenses(PrescriptionId). Bên đến sau luôn thất bại và KHÔNG trừ kho.
    /// </summary>
    public async Task<ServiceResult<DispenseDto>> DispenseAsync(int prescriptionId, CancellationToken ct = default)
    {
        guard.Require("xác nhận cấp phát thuốc", UserRole.Pharmacist);

        // Luôn đọc lại trạng thái MỚI NHẤT từ database, không tin trạng thái mà trình duyệt đang hiển thị.
        var prescription = await db.Prescriptions
            .Include(x => x.Items).ThenInclude(x => x.Medicine)
            .Include(x => x.Visit).ThenInclude(x => x.Invoices)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.PrescriptionId == prescriptionId, ct);
        if (prescription is null)
        {
            return ServiceResult<DispenseDto>.Failure("PRESCRIPTION_NOT_FOUND", "Không tìm thấy đơn thuốc.");
        }

        if (await db.Dispenses.AnyAsync(x => x.PrescriptionId == prescriptionId, ct))
        {
            return ServiceResult<DispenseDto>.Failure(
                "PRESCRIPTION_ALREADY_DISPENSED", "Đơn thuốc này đã được cấp phát trước đó.");
        }

        var invoice = prescription.Visit.Invoices.FirstOrDefault(x => x.Status != InvoiceStatus.Voided);
        var blockReason = GetDispenseBlockReason(prescription, invoice?.Status, invoice?.IsLocked ?? false);
        if (blockReason is not null)
        {
            return ServiceResult<DispenseDto>.Failure("DISPENSE_BLOCKED", blockReason);
        }

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId!.Value;
        var dispense = new Dispense
        {
            PrescriptionId = prescription.PrescriptionId,
            ConfirmedByUserId = userId,
            ConfirmedAtUtc = now,
            Status = DispenseStatus.Confirmed,
            Items = prescription.Items.Select(item => new DispenseItem
            {
                PrescriptionItemId = item.PrescriptionItemId,
                MedicineId = item.MedicineId,
                Quantity = item.Quantity,
                Unit = item.Medicine.Unit
            }).ToList()
        };
        db.Dispenses.Add(dispense);

        var stockChanges = new List<object>();
        foreach (var group in prescription.Items.GroupBy(x => x.MedicineId))
        {
            var medicine = group.First().Medicine;
            var quantity = group.Sum(x => x.Quantity);
            var previousStock = medicine.StockQuantity;

            medicine.StockQuantity -= quantity; // UPDATE ... WHERE RowVersion = <giá trị lúc đọc>
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                MedicineId = medicine.MedicineId,
                Dispense = dispense,
                QuantityChange = -quantity,
                Type = InventoryTransactionType.Dispense,
                Reference = $"Cấp phát đơn thuốc #{prescription.PrescriptionId}",
                OccurredAtUtc = now,
                PerformedByUserId = userId
            });
            stockChanges.Add(new { medicine.Name, PreviousStock = previousStock, NewStock = medicine.StockQuantity });
        }

        prescription.Status = PrescriptionStatus.Dispensed;
        prescription.StatusHistory.Add(new PrescriptionStatusHistory
        {
            FromStatus = PrescriptionStatus.ReadyForDispense,
            ToStatus = PrescriptionStatus.Dispensed,
            ChangedByUserId = userId,
            ChangedAtUtc = now,
            Reason = "Dược sĩ xác nhận cấp phát."
        });
        prescription.Visit.Status = VisitStatus.Completed;
        prescription.Visit.CompletedAtUtc = now;

        AuditLogWriter.Add(db, currentUser, "Prescription.Dispensed", nameof(Prescription), prescription.PrescriptionId,
            new { Status = PrescriptionStatus.ReadyForDispense },
            new { prescription.Status, StockChanges = stockChanges });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Tồn kho hoặc đơn thuốc đã bị giao dịch khác thay đổi sau lúc ta đọc -> hủy toàn bộ, không trừ kho.
            return ServiceResult<DispenseDto>.Failure("STOCK_CHANGED_RELOAD",
                "Tồn kho hoặc đơn thuốc vừa được thay đổi bởi giao dịch khác. Chưa cấp phát; hãy tải lại và thử lại.");
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<DispenseDto>.Failure(
                "PRESCRIPTION_ALREADY_DISPENSED", "Đơn thuốc vừa được dược sĩ khác xác nhận cấp phát.");
        }

        return ServiceResult<DispenseDto>.Success(
            new DispenseDto(dispense.DispenseId, prescription.PrescriptionId, dispense.ConfirmedByUserId,
                dispense.ConfirmedAtUtc, dispense.Status),
            "Cấp phát thành công. Tồn kho đã được trừ và lượt khám hoàn tất.");
    }

    // ------------------------------------------------------------------ QUY TẮC DÙNG CHUNG

    private static bool CanReturn(PrescriptionStatus status)
        => status is PrescriptionStatus.Issued or PrescriptionStatus.ReadyForDispense;

    /// <summary>Trả về null nếu cấp phát được; ngược lại là lý do (để giao diện giải thích vì sao nút bị khóa).</summary>
    private static string? GetDispenseBlockReason(Prescription prescription, InvoiceStatus? invoiceStatus, bool invoiceLocked)
    {
        if (prescription.Status == PrescriptionStatus.Dispensed)
        {
            return "Đơn thuốc đã được cấp phát.";
        }

        if (prescription.Status == PrescriptionStatus.Issued || invoiceStatus != InvoiceStatus.Paid || !invoiceLocked)
        {
            return "Chỉ được cấp phát sau khi hóa đơn đã thanh toán.";
        }

        if (prescription.Status != PrescriptionStatus.ReadyForDispense)
        {
            return $"Đơn thuốc đang ở trạng thái \"{prescription.Status.ToLabel()}\" nên chưa cấp phát được.";
        }

        if (prescription.Items.Count == 0)
        {
            return "Đơn thuốc không có thuốc nào.";
        }

        foreach (var group in prescription.Items.GroupBy(x => x.MedicineId))
        {
            var medicine = group.First().Medicine;
            var quantity = group.Sum(x => x.Quantity);
            if (!medicine.IsActive)
            {
                return $"Thuốc {medicine.Name} đã ngừng sử dụng. Hãy trả đơn về bác sĩ.";
            }

            if (medicine.StockQuantity < quantity)
            {
                return $"Không đủ tồn kho {medicine.Name}: cần {quantity:0.###} {medicine.Unit}, " +
                       $"còn {medicine.StockQuantity:0.###} {medicine.Unit}. Hãy nhập kho hoặc trả đơn về bác sĩ.";
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<PharmacyQueueItemDto>> QueryQueueAsync(
        System.Linq.Expressions.Expression<Func<Prescription, bool>> filter, bool newestFirst, CancellationToken ct)
    {
        var query = db.Prescriptions.AsNoTracking().Where(filter);
        query = newestFirst ? query.OrderByDescending(x => x.IssuedAtUtc) : query.OrderBy(x => x.IssuedAtUtc);

        return await query
            .Take(100)
            .Select(x => new PharmacyQueueItemDto(
                x.PrescriptionId,
                x.Visit.Appointment.Patient.FullName,
                x.Visit.Appointment.Patient.MedicalRecordNumber,
                x.Visit.Appointment.Doctor.DisplayName,
                x.IssuedAtUtc,
                x.Status,
                x.Items.Count,
                x.Items.Any(i => i.Medicine.StockQuantity < i.Quantity)))
            .ToListAsync(ct);
    }
}
