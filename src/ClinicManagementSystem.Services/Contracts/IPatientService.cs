using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

public interface IPatientService
{
    Task<IReadOnlyList<PatientListItemDto>> SearchAsync(string? keyword, CancellationToken ct = default);
    Task<PatientDetailDto?> GetAsync(int patientId, CancellationToken ct = default);
    Task<ServiceResult<int>> CreateAsync(CreatePatientDto dto, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(UpdatePatientDto dto, CancellationToken ct = default);
    Task<ServiceResult> AddAllergyAsync(AddAllergyDto dto, CancellationToken ct = default);
    Task<ServiceResult> DeactivateAllergyAsync(int allergyId, CancellationToken ct = default);

    Task<IReadOnlyList<GuardianListItemDto>> SearchGuardiansAsync(string? keyword, CancellationToken ct = default);
    Task<GuardianDetailDto?> GetGuardianAsync(int guardianId, CancellationToken ct = default);
    Task<GuardianListItemDto?> FindGuardianByPhoneAsync(string phone, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveGuardianAsync(SaveGuardianDto dto, CancellationToken ct = default);
}
