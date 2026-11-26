using System.Data;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Appointments;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class AppointmentService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IAppointmentService
{
    private const string SlotTakenMessage = "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.";

    public async Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(
        AppointmentFilterDto filter, CancellationToken ct = default)
    {
        guard.Require("xem lịch hẹn", UserRole.Admin, UserRole.Receptionist);

        var query = db.Appointments.AsNoTracking();
        if (filter.Date is { } date)
        {
            var (startUtc, endUtc) = ClinicClock.DayRangeUtc(date);
            query = query.Where(x => x.StartsAtUtc >= startUtc && x.StartsAtUtc < endUtc);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        var text = filter.Keyword?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var digits = CodeFormatter.NormalizePhone(text);
            query = query.Where(x =>
                x.Patient.FullName.Contains(text)
                || x.Patient.MedicalRecordNumber.Contains(text)
                || (digits.Length >= 3 && x.Patient.Guardian.PhoneNumber.Contains(digits)));
        }

        return await ProjectToListItem(query.OrderBy(x => x.StartsAtUtc).Take(200)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SlotDto>> GetSlotsAsync(int doctorId, DateOnly date, CancellationToken ct = default)
    {
        guard.Require("xem khung giờ trống", UserRole.Admin, UserRole.Receptionist);

        var (startUtc, endUtc) = ClinicClock.DayRangeUtc(date);
        var busyUtc = await db.Appointments.AsNoTracking()
            .Where(x => x.DoctorId == doctorId
                        && x.Status != AppointmentStatus.Cancelled
                        && x.StartsAtUtc < endUtc && x.EndsAtUtc > startUtc)
            .Select(x => new { x.StartsAtUtc, x.EndsAtUtc })
            .ToListAsync(ct);

        var busyLocal = busyUtc
            .Select(x => (ClinicClock.ToLocal(x.StartsAtUtc), ClinicClock.ToLocal(x.EndsAtUtc)))
            .ToList();
        return SlotCalculator.Build(date, busyLocal, ClinicClock.LocalNow);
    }

    /// <summary>
    /// Đặt lịch với BA lớp chống trùng:
    ///  (1) kiểm tra chồng lấn bằng truy vấn,
    ///  (2) chạy trong transaction SERIALIZABLE để hai request đồng thời không cùng "thấy trống",
    ///  (3) unique index trong database là chốt chặn cuối cùng.
    /// </summary>
    public async Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(
        CreateAppointmentDto dto, CancellationToken ct = default)
    {
        guard.Require("đặt lịch khám", UserRole.Admin, UserRole.Receptionist);
        ArgumentNullException.ThrowIfNull(dto);

        var startUtc = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(dto.EndsAtUtc, DateTimeKind.Utc);
        if (dto.PatientId <= 0 || dto.DoctorId <= 0 || endUtc <= startUtc)
        {
            return ServiceResult<AppointmentDto>.Failure(
                "APPOINTMENT_INVALID", "Vui lòng chọn bệnh nhi, bác sĩ và một khung giờ.");
        }

        if (startUtc <= DateTime.UtcNow)
        {
            return ServiceResult<AppointmentDto>.Failure("APPOINTMENT_IN_PAST", "Không thể đặt lịch vào khung giờ đã qua.");
        }

        // Không tin dữ liệu từ trình duyệt: người dùng có thể tự sửa form để gửi giờ "lạ" (08:07, 23:00...).
        if (!SlotCalculator.IsValidSlotStart(ClinicClock.ToLocal(startUtc))
            || (endUtc - startUtc).TotalMinutes != SlotCalculator.SlotMinutes)
        {
            return ServiceResult<AppointmentDto>.Failure(
                "APPOINTMENT_SLOT_INVALID", "Khung giờ không thuộc lịch làm việc của phòng khám.");
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (!await db.Patients.AnyAsync(x => x.PatientId == dto.PatientId && x.IsActive, ct))
            {
                return ServiceResult<AppointmentDto>.Failure("PATIENT_NOT_FOUND", "Không tìm thấy hồ sơ bệnh nhi đang hoạt động.");
            }

            if (!await db.Users.AnyAsync(x => x.UserId == dto.DoctorId && x.IsActive && x.Role == UserRole.Doctor, ct))
            {
                return ServiceResult<AppointmentDto>.Failure("DOCTOR_NOT_FOUND", "Không tìm thấy bác sĩ đang hoạt động.");
            }

            // Hai khoảng [A1,A2) và [B1,B2) giao nhau khi và chỉ khi A1 < B2 và A2 > B1.
            // Công thức này bắt được cả trường hợp chồng lấn MỘT PHẦN, không chỉ trùng giờ bắt đầu.
            var doctorBusy = await db.Appointments.AnyAsync(
                x => x.DoctorId == dto.DoctorId
                     && x.Status != AppointmentStatus.Cancelled
                     && x.StartsAtUtc < endUtc && x.EndsAtUtc > startUtc, ct);
            if (doctorBusy)
            {
                return ServiceResult<AppointmentDto>.Failure("APPOINTMENT_SLOT_TAKEN", SlotTakenMessage);
            }

            // Một bé không thể có hai lịch còn hiệu lực giao nhau (kể cả với hai bác sĩ khác nhau).
            var patientBusy = await db.Appointments.AnyAsync(
                x => x.PatientId == dto.PatientId
                     && (x.Status == AppointmentStatus.Booked || x.Status == AppointmentStatus.CheckedIn)
                     && x.StartsAtUtc < endUtc && x.EndsAtUtc > startUtc, ct);
            if (patientBusy)
            {
                return ServiceResult<AppointmentDto>.Failure(
                    "PATIENT_ALREADY_BOOKED", "Bệnh nhi này đã có lịch hẹn khác trong cùng khung giờ.");
            }

            var appointment = new Appointment
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                BookedByUserId = currentUser.UserId!.Value,
                StartsAtUtc = startUtc,
                EndsAtUtc = endUtc,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
                Status = AppointmentStatus.Booked,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync(ct); // lấy AppointmentId để ghi nhật ký

            AuditLogWriter.Add(db, currentUser, "Appointment.Created", nameof(Appointment), appointment.AppointmentId, null,
                new { appointment.PatientId, appointment.DoctorId, appointment.StartsAtUtc, appointment.EndsAtUtc, appointment.Status });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ServiceResult<AppointmentDto>.Success(
                new AppointmentDto(appointment.AppointmentId, appointment.PatientId, appointment.DoctorId,
                    appointment.StartsAtUtc, appointment.EndsAtUtc, appointment.Status),
                "Đặt lịch thành công.");
        }
        // Ra khỏi khối try mà chưa Commit -> `await using` tự ROLLBACK transaction.
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<AppointmentDto>.Failure("APPOINTMENT_SLOT_TAKEN", SlotTakenMessage);
        }
        catch (Exception exception) when (SqlServerErrorDetector.IsDeadlock(exception))
        {
            // Hai giao dịch Serializable tranh nhau: SQL Server hủy một bên. Với người dùng, ý nghĩa vẫn là "vừa có người đặt".
            return ServiceResult<AppointmentDto>.Failure("APPOINTMENT_SLOT_TAKEN", SlotTakenMessage);
        }
    }

    public async Task<ServiceResult> CancelAsync(int appointmentId, string? reason, CancellationToken ct = default)
    {
        guard.Require("hủy lịch hẹn", UserRole.Admin, UserRole.Receptionist);

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrEmpty(normalizedReason) || normalizedReason.Length > 500)
        {
            return ServiceResult.Failure("CANCEL_REASON_REQUIRED", "Vui lòng nhập lý do hủy lịch (tối đa 500 ký tự).");
        }

        var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
        if (appointment is null)
        {
            return ServiceResult.Failure("APPOINTMENT_NOT_FOUND", "Không tìm thấy lịch hẹn.");
        }

        // Máy trạng thái: chỉ lịch còn ở "Đã đặt" mới hủy được. Đã check-in thì đã có lượt khám, không hủy kiểu này.
        if (appointment.Status != AppointmentStatus.Booked)
        {
            return ServiceResult.Failure("APPOINTMENT_STATE_INVALID",
                $"Chỉ hủy được lịch ở trạng thái Đã đặt (hiện tại: {appointment.Status.ToLabel()}).");
        }

        var before = new { appointment.Status };
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = DateTime.UtcNow;
        appointment.CancelReason = normalizedReason;
        AuditLogWriter.Add(db, currentUser, "Appointment.Cancelled", nameof(Appointment), appointment.AppointmentId,
            before, new { appointment.Status, appointment.CancelReason });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion đã đổi: người khác vừa check-in/hủy lịch này giữa lúc ta đọc và lúc ta lưu.
            return ServiceResult.Failure("APPOINTMENT_CHANGED", "Lịch hẹn vừa được người khác cập nhật. Vui lòng tải lại.");
        }

        return ServiceResult.Success("Đã hủy lịch hẹn. Khung giờ được giải phóng.");
    }

    /// <summary>Phép chiếu dùng chung: biến Appointment thành dòng hiển thị (SQL chỉ SELECT đúng các cột cần).</summary>
    internal static IQueryable<AppointmentListItemDto> ProjectToListItem(IQueryable<Appointment> query)
        => query.Select(x => new AppointmentListItemDto(
            x.AppointmentId, x.StartsAtUtc, x.EndsAtUtc, x.Status,
            x.PatientId, x.Patient.MedicalRecordNumber, x.Patient.FullName,
            x.Patient.Guardian.FullName, x.Patient.Guardian.PhoneNumber,
            x.DoctorId, x.Doctor.DisplayName, x.Doctor.Specialty, x.Reason));
}
