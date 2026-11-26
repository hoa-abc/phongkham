using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Clinical;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class ReceptionService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IReceptionService
{
    private const string DefaultServiceCode = "PED-GEN";

    public async Task<IReadOnlyList<AppointmentListItemDto>> GetQueueAsync(DateOnly date, CancellationToken ct = default)
    {
        guard.Require("xem hàng chờ tiếp nhận", UserRole.Admin, UserRole.Receptionist);

        var (startUtc, endUtc) = ClinicClock.DayRangeUtc(date);
        var query = db.Appointments.AsNoTracking()
            .Where(x => x.Status == AppointmentStatus.Booked && x.StartsAtUtc >= startUtc && x.StartsAtUtc < endUtc)
            .OrderBy(x => x.StartsAtUtc);
        return await AppointmentService.ProjectToListItem(query).ToListAsync(ct);
    }

    public async Task<CheckInContextDto?> GetCheckInContextAsync(int appointmentId, CancellationToken ct = default)
    {
        guard.Require("tiếp nhận bệnh nhi", UserRole.Admin, UserRole.Receptionist);

        var appointment = await AppointmentService
            .ProjectToListItem(db.Appointments.AsNoTracking().Where(x => x.AppointmentId == appointmentId))
            .SingleOrDefaultAsync(ct);
        if (appointment is null)
        {
            return null;
        }

        var patient = await PatientContextLoader.LoadAsync(db, appointment.PatientId, ct);
        return patient is null ? null : new CheckInContextDto(appointment, patient);
    }

    /// <summary>
    /// Check-in = một đơn vị công việc gồm 4 thay đổi phải cùng thành công:
    /// đổi trạng thái lịch hẹn, tạo lượt khám, ghi sinh hiệu, chỉ định dịch vụ khám mặc định (+ nhật ký).
    /// Tất cả nằm trong MỘT lần SaveChanges nên EF Core tự bọc chúng trong một transaction.
    /// </summary>
    public async Task<ServiceResult<int>> CheckInAsync(CheckInDto dto, CancellationToken ct = default)
    {
        guard.Require("check-in bệnh nhi", UserRole.Admin, UserRole.Receptionist);

        // Khoảng hợp lệ rộng, mục đích bắt lỗi gõ nhầm (ví dụ 365 thay vì 36.5), không phải đánh giá y khoa.
        if (dto.WeightKg is < 1 or > 150 || dto.HeightCm is < 30 or > 200
            || dto.TemperatureC is < 30 or > 45 || dto.HeartRate is < 30 or > 250)
        {
            return ServiceResult<int>.Failure("VITALS_INVALID",
                "Sinh hiệu ngoài khoảng cho phép: cân nặng 1-150 kg, chiều cao 30-200 cm, nhiệt độ 30-45 °C, nhịp tim 30-250.");
        }

        var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.AppointmentId == dto.AppointmentId, ct);
        if (appointment is null)
        {
            return ServiceResult<int>.Failure("APPOINTMENT_NOT_FOUND", "Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.Booked)
        {
            return ServiceResult<int>.Failure("APPOINTMENT_STATE_INVALID",
                $"Lịch hẹn không còn ở trạng thái Đã đặt (hiện tại: {appointment.Status.ToLabel()}).");
        }

        if (DateOnly.FromDateTime(ClinicClock.ToLocal(appointment.StartsAtUtc)) != ClinicClock.Today)
        {
            return ServiceResult<int>.Failure("APPOINTMENT_NOT_TODAY", "Chỉ check-in được lịch hẹn của ngày hôm nay.");
        }

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId!.Value;
        var visit = new Visit
        {
            AppointmentId = appointment.AppointmentId,
            Status = VisitStatus.Waiting,
            ChiefComplaint = string.IsNullOrWhiteSpace(dto.Symptoms) ? appointment.Reason : dto.Symptoms.Trim(),
            CheckedInAtUtc = now,
            VitalSigns =
            {
                new VitalSign
                {
                    WeightKg = dto.WeightKg, HeightCm = dto.HeightCm, TemperatureC = dto.TemperatureC,
                    HeartRate = dto.HeartRate, RecordedAtUtc = now, RecordedByUserId = userId
                }
            }
        };

        var defaultService = await db.ClinicServices
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Code == DefaultServiceCode).ThenBy(x => x.ServiceId)
            .FirstOrDefaultAsync(ct);
        if (defaultService is not null)
        {
            visit.ServiceOrders.Add(new VisitServiceOrder
            {
                ServiceId = defaultService.ServiceId, Quantity = 1, UnitPriceSnapshot = defaultService.Price,
                OrderedByUserId = userId, OrderedAtUtc = now
            });
        }

        appointment.Status = AppointmentStatus.CheckedIn;
        db.Visits.Add(visit);
        AuditLogWriter.Add(db, currentUser, "Appointment.CheckedIn", "Appointment", appointment.AppointmentId,
            new { Status = AppointmentStatus.Booked },
            new { appointment.Status, dto.WeightKg, dto.HeightCm, dto.TemperatureC, dto.HeartRate });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<int>.Failure("APPOINTMENT_CHANGED", "Lịch hẹn vừa được người khác cập nhật. Vui lòng tải lại.");
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            // Unique index Visits(AppointmentId): hai quầy cùng check-in một lịch thì chỉ một bên thành công.
            return ServiceResult<int>.Failure("ALREADY_CHECKED_IN", "Lịch hẹn này đã được check-in trước đó.");
        }

        return ServiceResult<int>.Success(visit.VisitId, "Check-in thành công. Bệnh nhi đã vào hàng đợi khám.");
    }
}
