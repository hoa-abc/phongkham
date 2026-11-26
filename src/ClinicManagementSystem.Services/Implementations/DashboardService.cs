using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

/// <summary>Số liệu trang Tổng quan. Chỉ đọc; mỗi vai trò chỉ nhận phần số liệu thuộc phạm vi của mình.</summary>
public sealed class DashboardService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        guard.RequireSignedIn("xem trang tổng quan");

        var role = currentUser.Role!.Value;
        var userId = currentUser.UserId!.Value;
        var isDoctor = role == UserRole.Doctor;
        var seesMoney = role is UserRole.Admin or UserRole.Receptionist;

        var today = ClinicClock.Today;
        var (dayStart, dayEnd) = ClinicClock.DayRangeUtc(today);

        // Bác sĩ chỉ thấy lịch của mình; vai trò khác thấy toàn phòng khám.
        var todayAppointments = db.Appointments.AsNoTracking()
            .Where(x => x.StartsAtUtc >= dayStart && x.StartsAtUtc < dayEnd && x.Status != AppointmentStatus.Cancelled)
            .Where(x => !isDoctor || x.DoctorId == userId);

        var totalToday = await todayAppointments.CountAsync(ct);
        var checkedIn = await todayAppointments.CountAsync(x => x.Status != AppointmentStatus.Booked && x.Status != AppointmentStatus.NoShow, ct);
        var schedule = await AppointmentService.ProjectToListItem(todayAppointments.OrderBy(x => x.StartsAtUtc)).ToListAsync(ct);

        var waiting = await db.Visits.AsNoTracking()
            .CountAsync(x => x.Status == VisitStatus.Waiting && (!isDoctor || x.Appointment.DoctorId == userId), ct);
        var returned = await db.Prescriptions.AsNoTracking()
            .CountAsync(x => x.Status == PrescriptionStatus.ReturnedToDoctor && (!isDoctor || x.CreatedByDoctorId == userId), ct);
        var unpaid = await db.Invoices.AsNoTracking().CountAsync(x => x.Status == InvoiceStatus.Unpaid, ct);
        var readyToDispense = await db.Prescriptions.AsNoTracking().CountAsync(x => x.Status == PrescriptionStatus.ReadyForDispense, ct);
        var totalPatients = await db.Patients.AsNoTracking().CountAsync(x => x.IsActive, ct);

        var lowStock = await db.Medicines.AsNoTracking()
            .Where(x => x.IsActive && x.StockQuantity <= x.ReorderLevel)
            .OrderBy(x => x.StockQuantity)
            .Select(x => new MedicineDto(
                x.MedicineId, x.Code, x.Name, x.ActiveIngredient, x.TherapeuticGroupKey, x.Strength, x.DosageForm,
                x.Unit, x.UnitPrice, x.StockQuantity, x.ReorderLevel, x.IsActive))
            .ToListAsync(ct);

        // ----- Thống kê 6 tháng gần nhất -----
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var rangeStart = ClinicClock.DayRangeUtc(firstMonth).StartUtc;

        var visitDates = await db.Visits.AsNoTracking()
            .Where(x => x.Appointment.StartsAtUtc >= rangeStart && x.Status != VisitStatus.Cancelled)
            .Select(x => x.Appointment.StartsAtUtc)
            .ToListAsync(ct);
        var patientDates = await db.Patients.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= rangeStart)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var payments = seesMoney
            ? await db.Payments.AsNoTracking()
                .Where(x => x.Status == PaymentStatus.Succeeded && x.PaidAtUtc >= rangeStart)
                .Select(x => new { x.PaidAtUtc, x.Amount })
                .ToListAsync(ct)
            : [];

        // Gom nhóm theo THÁNG GIỜ VIỆT NAM. Làm trong bộ nhớ vì chỉ vài trăm dòng và cần đổi múi giờ trước khi gom.
        static (int Year, int Month) MonthOf(DateTime utc)
        {
            var local = ClinicClock.ToLocal(utc);
            return (local.Year, local.Month);
        }

        var monthlyStats = Enumerable.Range(0, 6)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month =>
            {
                var key = (month.Year, month.Month);
                return new MonthlyStatDto(
                    $"T{month.Month}/{month.Year % 100:00}",
                    visitDates.Count(x => MonthOf(x) == key),
                    patientDates.Count(x => MonthOf(x) == key),
                    payments.Where(x => MonthOf(x.PaidAtUtc) == key).Sum(x => x.Amount));
            })
            .ToList();

        var currentMonth = (today.Year, today.Month);
        var revenueToday = payments.Where(x => x.PaidAtUtc >= dayStart && x.PaidAtUtc < dayEnd).Sum(x => x.Amount);
        var revenueMonth = payments.Where(x => MonthOf(x.PaidAtUtc) == currentMonth).Sum(x => x.Amount);

        var pendingInvoices = seesMoney
            ? await BillingService.ProjectToListItem(
                    db.Invoices.AsNoTracking().Where(x => x.Status == InvoiceStatus.Unpaid).OrderBy(x => x.CreatedAtUtc).Take(5))
                .ToListAsync(ct)
            : [];

        var recentActivity = role == UserRole.Admin
            ? await db.AuditLogs.AsNoTracking()
                .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.AuditLogId)
                .Take(8)
                .Select(x => new AuditLogDto(x.AuditLogId, x.OccurredAtUtc, x.ActorNameSnapshot, x.Action, x.EntityName, x.EntityId, null, null))
                .ToListAsync(ct)
            : [];

        return new DashboardDto(
            role, totalToday, checkedIn, waiting, unpaid, readyToDispense, returned, lowStock.Count, totalPatients,
            revenueToday, revenueMonth, schedule, pendingInvoices, lowStock, monthlyStats, recentActivity);
    }
}
