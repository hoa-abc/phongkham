using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

public sealed record AuditLogDto(
    int AuditLogId,
    DateTime OccurredAtUtc,
    string ActorName,
    string Action,
    string EntityName,
    string EntityId,
    string? BeforeJson,
    string? AfterJson);

public sealed record AuditSearchDto(string? Keyword, string? EntityName, DateOnly? From, DateOnly? To, int Page = 1);

public sealed record MonthlyStatDto(string Label, int Visits, int NewPatients, decimal Revenue);

/// <summary>Số liệu trang Tổng quan. Mỗi vai trò chỉ dùng phần liên quan tới mình.</summary>
public sealed record DashboardDto(
    UserRole Role,
    int TodayAppointments,
    int CheckedInToday,
    int WaitingForDoctor,
    int UnpaidInvoices,
    int ReadyToDispense,
    int ReturnedPrescriptions,
    int LowStockMedicines,
    int TotalPatients,
    decimal RevenueToday,
    decimal RevenueThisMonth,
    IReadOnlyList<AppointmentListItemDto> TodaySchedule,
    IReadOnlyList<InvoiceListItemDto> PendingInvoices,
    IReadOnlyList<MedicineDto> LowStock,
    IReadOnlyList<MonthlyStatDto> MonthlyStats,
    IReadOnlyList<AuditLogDto> RecentActivity);
