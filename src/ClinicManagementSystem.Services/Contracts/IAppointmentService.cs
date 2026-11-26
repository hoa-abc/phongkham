using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(AppointmentFilterDto filter, CancellationToken ct = default);
    Task<IReadOnlyList<SlotDto>> GetSlotsAsync(int doctorId, DateOnly date, CancellationToken ct = default);
    Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(CreateAppointmentDto dto, CancellationToken ct = default);
    Task<ServiceResult> CancelAsync(int appointmentId, string? reason, CancellationToken ct = default);
}

public interface IReceptionService
{
    Task<IReadOnlyList<AppointmentListItemDto>> GetQueueAsync(DateOnly date, CancellationToken ct = default);
    Task<CheckInContextDto?> GetCheckInContextAsync(int appointmentId, CancellationToken ct = default);
    Task<ServiceResult<int>> CheckInAsync(CheckInDto dto, CancellationToken ct = default);
}
