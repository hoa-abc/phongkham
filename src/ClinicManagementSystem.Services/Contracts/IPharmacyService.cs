using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

/// <summary>Mọi thao tác của dược sĩ trên đơn thuốc. Dược sĩ KHÔNG có hàm nào để sửa nội dung đơn.</summary>
public interface IPharmacyService
{
    Task<IReadOnlyList<PharmacyQueueItemDto>> GetQueueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PharmacyQueueItemDto>> GetHistoryAsync(CancellationToken ct = default);
    Task<PharmacyPrescriptionDto?> GetAsync(int prescriptionId, CancellationToken ct = default);
    Task<ServiceResult<PrescriptionDto>> ReturnToDoctorAsync(int prescriptionId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<DispenseDto>> DispenseAsync(int prescriptionId, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<IReadOnlyList<MedicineDto>> GetCatalogAsync(bool includeInactive, CancellationToken ct = default);
    Task<MedicineDto?> GetAsync(int medicineId, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveMedicineAsync(SaveMedicineDto dto, CancellationToken ct = default);
    Task<ServiceResult> AdjustStockAsync(StockAdjustmentDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(int? medicineId, CancellationToken ct = default);
}
