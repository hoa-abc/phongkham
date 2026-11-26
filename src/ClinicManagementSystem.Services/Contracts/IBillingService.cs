using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

public interface IBillingService
{
    Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(InvoiceStatus? status, CancellationToken ct = default);
    Task<InvoiceDetailDto?> GetAsync(int invoiceId, CancellationToken ct = default);
    Task<ServiceResult<PaymentDto>> ProcessPaymentAsync(int invoiceId, PaymentMethod method, CancellationToken ct = default);
    Task<ServiceResult> VoidAsync(int invoiceId, string? reason, CancellationToken ct = default);
}

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ClinicServiceDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<ClinicServiceDto?> GetAsync(int serviceId, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveAsync(SaveClinicServiceDto dto, CancellationToken ct = default);
}
