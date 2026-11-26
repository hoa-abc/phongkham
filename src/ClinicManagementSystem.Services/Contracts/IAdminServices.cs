using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

public interface IAuditService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditSearchDto search, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}
