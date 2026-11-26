using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

public interface IConsultationService
{
    Task<IReadOnlyList<DoctorQueueItemDto>> GetQueueAsync(CancellationToken ct = default);
    Task<ConsultationDto?> GetAsync(int visitId, CancellationToken ct = default);
    Task<ServiceResult> StartAsync(int visitId, CancellationToken ct = default);
    Task<ServiceResult> SaveDraftAsync(SaveConsultationDto dto, CancellationToken ct = default);
    Task<ServiceResult> CompleteAsync(SaveConsultationDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<VisitHistoryItemDto>> GetHistoryAsync(bool onlyWithPrescription, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineOptionDto>> GetMedicineOptionsAsync(CancellationToken ct = default);
}
