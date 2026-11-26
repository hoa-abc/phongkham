using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

public sealed record PharmacyQueueItemDto(
    int PrescriptionId,
    string PatientName,
    string MedicalRecordNumber,
    string DoctorName,
    DateTime? IssuedAtUtc,
    PrescriptionStatus Status,
    int ItemCount,
    bool HasStockIssue);

public sealed record PharmacyItemDto(
    string MedicineName,
    string Strength,
    string Unit,
    decimal Quantity,
    decimal StockQuantity,
    string? DosageInstructions,
    bool IsEnough);

public sealed record PrescriptionHistoryDto(
    DateTime ChangedAtUtc,
    PrescriptionStatus FromStatus,
    PrescriptionStatus ToStatus,
    string ChangedByName,
    string? Reason);

public sealed record PharmacyPrescriptionDto(
    int PrescriptionId,
    PrescriptionStatus Status,
    PatientContextDto Patient,
    string DoctorName,
    string? Diagnosis,
    IReadOnlyList<PharmacyItemDto> Items,
    InvoiceStatus? InvoiceStatus,
    bool CanDispense,
    string? BlockReason,
    bool CanReturn,
    string? ReturnReason,
    IReadOnlyList<PrescriptionHistoryDto> History);

public sealed record DispenseDto(
    int DispenseId,
    int PrescriptionId,
    int ConfirmedByUserId,
    DateTime ConfirmedAtUtc,
    DispenseStatus Status);

public sealed record MedicineDto(
    int MedicineId,
    string Code,
    string Name,
    string ActiveIngredient,
    string? TherapeuticGroupKey,
    string Strength,
    string DosageForm,
    string Unit,
    decimal UnitPrice,
    decimal StockQuantity,
    decimal ReorderLevel,
    bool IsActive)
{
    public bool IsLowStock => StockQuantity <= ReorderLevel;
}

public sealed record SaveMedicineDto(
    int? MedicineId,
    string Code,
    string Name,
    string ActiveIngredient,
    string? TherapeuticGroup,
    string Strength,
    string DosageForm,
    string Unit,
    decimal UnitPrice,
    decimal ReorderLevel,
    bool IsActive);

/// <summary>Số dương = nhập thêm, số âm = giảm tồn. Luôn phải kèm lý do.</summary>
public sealed record StockAdjustmentDto(int MedicineId, decimal QuantityChange, string Reason);

public sealed record InventoryTransactionDto(
    int InventoryTransactionId,
    DateTime OccurredAtUtc,
    string MedicineName,
    string Unit,
    decimal QuantityChange,
    InventoryTransactionType Type,
    string? Reference,
    string PerformedByName);
