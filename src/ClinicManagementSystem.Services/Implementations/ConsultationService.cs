using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Clinical;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

/// <summary>
/// Nghiệp vụ của BÁC SĨ: hàng đợi khám, ghi nhận lượt khám, kê đơn, phát hành đơn và hoàn tất khám.
/// Bác sĩ chỉ thao tác được trên lượt khám của CHÍNH MÌNH (so DoctorId với người đang đăng nhập).
/// </summary>
public sealed class ConsultationService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IConsultationService
{
    private const decimal MaxQuantityPerLine = 1000;

    public async Task<IReadOnlyList<DoctorQueueItemDto>> GetQueueAsync(CancellationToken ct = default)
    {
        guard.Require("xem hàng đợi khám", UserRole.Doctor);
        var doctorId = currentUser.UserId!.Value;

        return await db.Visits.AsNoTracking()
            .Where(x => x.Appointment.DoctorId == doctorId
                        && (x.Status == VisitStatus.Waiting || x.Status == VisitStatus.InConsultation))
            .OrderBy(x => x.Appointment.StartsAtUtc)
            .Select(x => new DoctorQueueItemDto(
                x.VisitId,
                x.Appointment.StartsAtUtc,
                x.Appointment.Patient.MedicalRecordNumber,
                x.Appointment.Patient.FullName,
                x.Appointment.Patient.DateOfBirth,
                x.Status,
                x.Prescription == null ? null : x.Prescription.Status,
                x.Prescription == null ? null : x.Prescription.ReturnReason,
                x.Appointment.Patient.Allergies.Any(a => a.IsActive)))
            .ToListAsync(ct);
    }

    public async Task<ConsultationDto?> GetAsync(int visitId, CancellationToken ct = default)
    {
        guard.Require("xem lượt khám", UserRole.Doctor);

        var visit = await db.Visits.AsNoTracking()
            .Include(x => x.Appointment)
            .Include(x => x.ServiceOrders)
            .Include(x => x.Prescription!).ThenInclude(x => x.Items).ThenInclude(x => x.Medicine)
            .AsSplitQuery() // tách thành vài câu SELECT nhỏ thay vì một câu JOIN "nổ" số dòng
            .SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        if (visit is null)
        {
            return null;
        }

        var patientId = visit.Appointment.PatientId;
        var patient = await PatientContextLoader.LoadAsync(db, patientId, ct);
        if (patient is null)
        {
            return null;
        }

        var items = (visit.Prescription?.Items ?? [])
            .Select(x => new PrescriptionLineDto(
                x.MedicineId, x.Medicine.Name, x.Medicine.Strength, x.Medicine.Unit, x.Quantity,
                x.DosageInstructions, x.Medicine.StockQuantity, x.Medicine.UnitPrice))
            .ToList();
        var warnings = await PrescriptionWarningEvaluator.EvaluateAsync(
            db, patientId, items.Select(x => new PrescriptionItemDto(x.MedicineId, x.Quantity)).ToList(), ct);

        var previousVisits = await db.Visits.AsNoTracking()
            .Where(x => x.Appointment.PatientId == patientId && x.VisitId != visitId
                        && x.Status != VisitStatus.Waiting && x.Status != VisitStatus.Cancelled)
            .OrderByDescending(x => x.Appointment.StartsAtUtc)
            .Take(5)
            .Select(x => new PatientVisitSummaryDto(
                x.VisitId, x.Appointment.StartsAtUtc, x.Appointment.Doctor.DisplayName, x.Diagnosis, x.Status))
            .ToListAsync(ct);

        string? readOnlyReason = null;
        if (visit.Appointment.DoctorId != currentUser.UserId)
        {
            readOnlyReason = "Lượt khám này thuộc bác sĩ khác nên chỉ xem được.";
        }
        else if (visit.Status == VisitStatus.Waiting)
        {
            readOnlyReason = "Hãy bấm \"Bắt đầu khám\" để ghi nhận nội dung khám.";
        }
        else if (visit.Status != VisitStatus.InConsultation)
        {
            readOnlyReason = $"Lượt khám đang ở trạng thái \"{visit.Status.ToLabel()}\" nên không còn chỉnh sửa được.";
        }

        return new ConsultationDto(
            visit.VisitId, visit.Status, patient, visit.Appointment.StartsAtUtc,
            visit.ChiefComplaint, visit.ClinicalNotes, visit.Diagnosis, visit.TreatmentPlan,
            items, visit.ServiceOrders.Select(x => x.ServiceId).ToList(),
            visit.Prescription?.Status, visit.Prescription?.ReturnReason,
            warnings, previousVisits, readOnlyReason is null, readOnlyReason);
    }

    public async Task<ServiceResult> StartAsync(int visitId, CancellationToken ct = default)
    {
        guard.Require("bắt đầu khám", UserRole.Doctor);

        var visit = await db.Visits.Include(x => x.Appointment).SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        if (visit is null)
        {
            return ServiceResult.Failure("VISIT_NOT_FOUND", "Không tìm thấy lượt khám.");
        }

        if (visit.Appointment.DoctorId != currentUser.UserId)
        {
            return ServiceResult.Failure("VISIT_NOT_ASSIGNED", "Lượt khám này được phân cho bác sĩ khác.");
        }

        if (visit.Status != VisitStatus.Waiting)
        {
            return ServiceResult.Failure("VISIT_STATE_INVALID", "Lượt khám không còn ở trạng thái chờ khám.");
        }

        visit.Status = VisitStatus.InConsultation;
        visit.StartedAtUtc = DateTime.UtcNow;
        visit.Appointment.Status = AppointmentStatus.InConsultation;
        AuditLogWriter.Add(db, currentUser, "Visit.Started", nameof(Visit), visit.VisitId,
            new { Status = VisitStatus.Waiting }, new { visit.Status });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Failure("VISIT_CHANGED", "Lượt khám vừa được cập nhật ở nơi khác. Vui lòng tải lại.");
        }

        return ServiceResult.Success("Đã bắt đầu khám.");
    }

    public async Task<ServiceResult> SaveDraftAsync(SaveConsultationDto dto, CancellationToken ct = default)
    {
        guard.Require("lưu nội dung khám", UserRole.Doctor);

        var (visit, error) = await LoadEditableVisitAsync(dto.VisitId, ct);
        if (visit is null)
        {
            return error!;
        }

        var applyError = await ApplyChangesAsync(visit, dto, ct);
        if (applyError is not null)
        {
            return applyError;
        }

        AuditLogWriter.Add(db, currentUser, "Visit.DraftSaved", nameof(Visit), visit.VisitId, null,
            new { ItemCount = visit.Prescription?.Items.Count ?? 0, ServiceCount = visit.ServiceOrders.Count });
        return await SaveAsync("Đã lưu nháp nội dung khám và đơn thuốc.", ct);
    }

    /// <summary>
    /// "Phát hành đơn và hoàn tất khám". Trong MỘT transaction:
    ///   lưu nội dung khám -> đơn thuốc chuyển Issued -> lập hóa đơn -> lượt khám chuyển Chờ thanh toán.
    /// Nếu bất kỳ bước nào lỗi, không có gì được ghi (không có "khám xong mà thiếu hóa đơn").
    /// </summary>
    public async Task<ServiceResult> CompleteAsync(SaveConsultationDto dto, CancellationToken ct = default)
    {
        guard.Require("hoàn tất khám và phát hành đơn", UserRole.Doctor);

        var (visit, error) = await LoadEditableVisitAsync(dto.VisitId, ct);
        if (visit is null)
        {
            return error!;
        }

        var applyError = await ApplyChangesAsync(visit, dto, ct);
        if (applyError is not null)
        {
            return applyError;
        }

        var prescription = visit.Prescription;
        var hasItems = prescription is { Items.Count: > 0 };

        // Kiểm tra điều kiện hoàn tất. Nếu chưa đạt: vẫn LƯU NHÁP những gì bác sĩ vừa nhập rồi mới báo lỗi,
        // để bác sĩ không phải gõ lại.
        string? blockerCode = null, blockerMessage = null;
        if (string.IsNullOrWhiteSpace(visit.Diagnosis))
        {
            (blockerCode, blockerMessage) = ("DIAGNOSIS_REQUIRED", "Vui lòng nhập chẩn đoán trước khi hoàn tất khám.");
        }
        else if (visit.ServiceOrders.Count == 0)
        {
            (blockerCode, blockerMessage) = ("SERVICE_REQUIRED", "Lượt khám cần ít nhất một dịch vụ để lập hóa đơn.");
        }
        else if (hasItems)
        {
            var warnings = await PrescriptionWarningEvaluator.EvaluateAsync(
                db, visit.Appointment.PatientId,
                prescription!.Items.Select(x => new PrescriptionItemDto(x.MedicineId, x.Quantity)).ToList(), ct);

            // Cảnh báo dị ứng không cấm kê đơn, nhưng bác sĩ phải xác nhận ĐÃ RÀ SOÁT thì mới phát hành được.
            if (warnings.Any(x => x.Severity == WarningSeverity.Red) && !dto.AcknowledgeWarnings)
            {
                (blockerCode, blockerMessage) = ("ALLERGY_ACK_REQUIRED",
                    "Đơn có thuốc khớp hồ sơ dị ứng. Hãy rà soát và đánh dấu xác nhận trước khi phát hành.");
            }
        }

        if (blockerCode is not null)
        {
            var draft = await SaveAsync("Đã lưu nháp.", ct);
            return draft.Succeeded
                ? ServiceResult.Failure(blockerCode, blockerMessage + " (Nội dung đã được lưu nháp.)")
                : draft;
        }

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId!.Value;
        if (prescription is not null)
        {
            var from = prescription.Status;
            prescription.Status = hasItems ? PrescriptionStatus.Issued : PrescriptionStatus.Cancelled;
            prescription.IssuedAtUtc = hasItems ? now : null;
            prescription.ReturnReason = null;
            prescription.ReturnedByUserId = null;
            prescription.StatusHistory.Add(new PrescriptionStatusHistory
            {
                FromStatus = from,
                ToStatus = prescription.Status,
                ChangedByUserId = userId,
                ChangedAtUtc = now,
                Reason = !hasItems ? "Bác sĩ hủy đơn (không còn thuốc)."
                    : from == PrescriptionStatus.ReturnedToDoctor ? "Bác sĩ đã chỉnh sửa và phát hành lại đơn."
                    : "Bác sĩ phát hành đơn."
            });
        }

        var invoice = InvoiceBuilder.Upsert(visit, userId, now);
        var oldStatus = visit.Status;
        visit.Status = VisitStatus.AwaitingPayment;
        visit.Appointment.Status = AppointmentStatus.Completed;

        AuditLogWriter.Add(db, currentUser, "Visit.ConsultationCompleted", nameof(Visit), visit.VisitId,
            new { Status = oldStatus },
            new
            {
                visit.Status,
                visit.Diagnosis,
                PrescriptionStatus = prescription?.Status,
                InvoiceTotal = invoice.TotalAmount
            });

        return await SaveAsync(
            hasItems ? "Đã phát hành đơn và chuyển hóa đơn sang quầy thu ngân." : "Đã hoàn tất khám và chuyển hóa đơn sang quầy thu ngân.",
            ct);
    }

    public async Task<IReadOnlyList<VisitHistoryItemDto>> GetHistoryAsync(bool onlyWithPrescription, CancellationToken ct = default)
    {
        guard.Require("xem lịch sử khám", UserRole.Doctor);
        var doctorId = currentUser.UserId!.Value;

        var query = db.Visits.AsNoTracking().Where(x => x.Appointment.DoctorId == doctorId && x.Status != VisitStatus.Waiting);
        if (onlyWithPrescription)
        {
            query = query.Where(x => x.Prescription != null);
        }

        return await query
            .OrderByDescending(x => x.Appointment.StartsAtUtc)
            .Take(100)
            .Select(x => new VisitHistoryItemDto(
                x.VisitId, x.Appointment.StartsAtUtc, x.Appointment.Patient.MedicalRecordNumber,
                x.Appointment.Patient.FullName, x.Appointment.Doctor.DisplayName, x.Diagnosis, x.Status,
                x.Prescription == null ? null : x.Prescription.Status))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicineOptionDto>> GetMedicineOptionsAsync(CancellationToken ct = default)
    {
        guard.Require("xem danh mục thuốc", UserRole.Doctor);
        return await db.Medicines.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new MedicineOptionDto(x.MedicineId, x.Name, x.Strength, x.Unit, x.StockQuantity, x.UnitPrice))
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ HÀM PHỤ

    /// <summary>Nạp lượt khám kèm mọi dữ liệu liên quan và kiểm tra bác sĩ này có được sửa không.</summary>
    private async Task<(Visit? Visit, ServiceResult? Error)> LoadEditableVisitAsync(int visitId, CancellationToken ct)
    {
        var visit = await db.Visits
            .Include(x => x.Appointment)
            .Include(x => x.ServiceOrders).ThenInclude(x => x.Service)
            .Include(x => x.Prescription!).ThenInclude(x => x.Items).ThenInclude(x => x.Medicine)
            .Include(x => x.Invoices).ThenInclude(x => x.Lines)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.VisitId == visitId, ct);

        if (visit is null)
        {
            return (null, ServiceResult.Failure("VISIT_NOT_FOUND", "Không tìm thấy lượt khám."));
        }

        if (visit.Appointment.DoctorId != currentUser.UserId)
        {
            return (null, ServiceResult.Failure("VISIT_NOT_ASSIGNED", "Lượt khám này được phân cho bác sĩ khác."));
        }

        if (visit.Status != VisitStatus.InConsultation)
        {
            return (null, ServiceResult.Failure("VISIT_STATE_INVALID",
                $"Lượt khám đang ở trạng thái \"{visit.Status.ToLabel()}\" nên không chỉnh sửa được."));
        }

        // Hóa đơn đã thu tiền thì bị khóa. Muốn sửa đơn (khi dược sĩ trả về) phải hủy/hoàn hóa đơn trước
        // bằng nghiệp vụ điều chỉnh ở quầy thu ngân - hệ thống không tự ý mở khóa hóa đơn đã thu tiền.
        if (visit.Invoices.Any(x => x.Status == InvoiceStatus.Paid))
        {
            return (null, ServiceResult.Failure("INVOICE_LOCKED_REQUIRES_ADJUSTMENT",
                "Hóa đơn của lượt khám đã thanh toán và bị khóa. Đề nghị quầy thu ngân hủy/hoàn hóa đơn trước khi sửa đơn thuốc."));
        }

        return (visit, null);
    }

    /// <summary>Áp dữ liệu từ form lên các entity đang được theo dõi (chưa lưu). Trả về lỗi nếu dữ liệu sai.</summary>
    private async Task<ServiceResult?> ApplyChangesAsync(Visit visit, SaveConsultationDto dto, CancellationToken ct)
    {
        var inputItems = dto.Items ?? [];
        if (inputItems.Any(x => x.MedicineId <= 0 || x.Quantity <= 0 || x.Quantity > MaxQuantityPerLine))
        {
            return ServiceResult.Failure("PRESCRIPTION_ITEMS_INVALID",
                $"Mỗi dòng thuốc phải chọn thuốc và có số lượng từ 0 đến {MaxQuantityPerLine:0}.");
        }

        // Gộp các dòng trùng thuốc thành một dòng (cộng số lượng).
        var items = inputItems
            .GroupBy(x => x.MedicineId)
            .Select(g => new PrescriptionItemInputDto(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Select(x => x.DosageInstructions?.Trim()).FirstOrDefault(x => !string.IsNullOrEmpty(x))))
            .ToList();

        var medicineIds = items.Select(x => x.MedicineId).ToList();
        var medicines = await db.Medicines
            .Where(x => medicineIds.Contains(x.MedicineId) && x.IsActive)
            .ToDictionaryAsync(x => x.MedicineId, ct);
        if (medicines.Count != medicineIds.Count)
        {
            return ServiceResult.Failure("MEDICINE_NOT_AVAILABLE", "Có thuốc không tồn tại hoặc đã ngừng sử dụng.");
        }

        var serviceIds = (dto.ServiceIds ?? []).Distinct().ToList();
        var services = await db.ClinicServices
            .Where(x => serviceIds.Contains(x.ServiceId))
            .ToDictionaryAsync(x => x.ServiceId, ct);
        if (services.Count != serviceIds.Count)
        {
            return ServiceResult.Failure("SERVICE_NOT_FOUND", "Có dịch vụ không tồn tại trong danh mục.");
        }

        visit.ChiefComplaint = Clean(dto.ChiefComplaint, 1000);
        visit.ClinicalNotes = Clean(dto.ClinicalNotes, 4000);
        visit.Diagnosis = Clean(dto.Diagnosis, 1000);
        visit.TreatmentPlan = Clean(dto.TreatmentPlan, 2000);

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId!.Value;

        // --- Dịch vụ chỉ định: so sánh cũ/mới, chỉ thêm cái mới và bỏ cái bị bỏ chọn ---
        foreach (var removed in visit.ServiceOrders.Where(x => !serviceIds.Contains(x.ServiceId)).ToList())
        {
            visit.ServiceOrders.Remove(removed);
        }

        foreach (var serviceId in serviceIds.Where(id => visit.ServiceOrders.All(x => x.ServiceId != id)))
        {
            var service = services[serviceId];
            if (!service.IsActive)
            {
                return ServiceResult.Failure("SERVICE_INACTIVE", $"Dịch vụ \"{service.Name}\" đã ngừng áp dụng.");
            }

            visit.ServiceOrders.Add(new VisitServiceOrder
            {
                ServiceId = service.ServiceId, Service = service, Quantity = 1, UnitPriceSnapshot = service.Price,
                OrderedByUserId = userId, OrderedAtUtc = now
            });
        }

        // --- Đơn thuốc: thay toàn bộ dòng cũ bằng dòng mới ---
        if (visit.Prescription is null && items.Count > 0)
        {
            visit.Prescription = new Prescription
            {
                CreatedByDoctorId = userId, Status = PrescriptionStatus.Draft, CreatedAtUtc = now
            };
        }

        if (visit.Prescription is { } prescription)
        {
            prescription.Items.Clear();
            foreach (var item in items)
            {
                prescription.Items.Add(new PrescriptionItem
                {
                    // Gán CẢ khóa ngoại lẫn navigation: EF Core chỉ tự điền MedicineId từ navigation lúc SaveChanges,
                    // trong khi bước tính cảnh báo dị ứng bên dưới cần MedicineId ngay bây giờ.
                    MedicineId = item.MedicineId,
                    Medicine = medicines[item.MedicineId],
                    Quantity = item.Quantity,
                    DosageInstructions = Clean(item.DosageInstructions, 1000)
                });
            }
        }

        return null;
    }

    private async Task<ServiceResult> SaveAsync(string successMessage, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult.Success(successMessage);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Failure("VISIT_CHANGED",
                "Lượt khám hoặc đơn thuốc vừa được cập nhật ở nơi khác. Vui lòng tải lại trước khi lưu.");
        }
    }

    private static string? Clean(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length > maxLength ? text[..maxLength] : text;
    }
}
