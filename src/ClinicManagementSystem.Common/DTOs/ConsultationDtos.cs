using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

public sealed record DoctorQueueItemDto(
    int VisitId,
    DateTime StartsAtUtc,
    string MedicalRecordNumber,
    string PatientName,
    DateOnly DateOfBirth,
    VisitStatus VisitStatus,
    PrescriptionStatus? PrescriptionStatus,
    string? ReturnReason,
    bool HasAllergies);

/// <summary>Một dòng thuốc bác sĩ nhập trên form kê đơn.</summary>
public sealed record PrescriptionItemInputDto(int MedicineId, decimal Quantity, string? DosageInstructions);

/// <summary>Số lượng tính theo đơn vị tồn kho của thuốc (chai, gói, viên...).</summary>
public sealed record PrescriptionItemDto(int MedicineId, decimal Quantity);

public sealed record PrescriptionLineDto(
    int MedicineId,
    string MedicineName,
    string Strength,
    string Unit,
    decimal Quantity,
    string? DosageInstructions,
    decimal StockQuantity,
    decimal UnitPrice);

public sealed record PrescriptionWarningDto(
    WarningCode Code,
    WarningSeverity Severity,
    int MedicineId,
    string MedicineName,
    string Message,
    decimal RequestedQuantity,
    decimal StockQuantity);

public sealed record MedicineOptionDto(
    int MedicineId,
    string Name,
    string Strength,
    string Unit,
    decimal StockQuantity,
    decimal UnitPrice);

public sealed record ConsultationDto(
    int VisitId,
    VisitStatus VisitStatus,
    PatientContextDto Patient,
    DateTime StartsAtUtc,
    string? ChiefComplaint,
    string? ClinicalNotes,
    string? Diagnosis,
    string? TreatmentPlan,
    IReadOnlyList<PrescriptionLineDto> Items,
    IReadOnlyList<int> ServiceIds,
    PrescriptionStatus? PrescriptionStatus,
    string? ReturnReason,
    IReadOnlyList<PrescriptionWarningDto> Warnings,
    IReadOnlyList<PatientVisitSummaryDto> PreviousVisits,
    bool CanEdit,
    string? ReadOnlyReason);

public sealed record SaveConsultationDto(
    int VisitId,
    string? ChiefComplaint,
    string? ClinicalNotes,
    string? Diagnosis,
    string? TreatmentPlan,
    IReadOnlyList<PrescriptionItemInputDto> Items,
    IReadOnlyList<int> ServiceIds,
    bool AcknowledgeWarnings);

public sealed record VisitHistoryItemDto(
    int VisitId,
    DateTime StartsAtUtc,
    string MedicalRecordNumber,
    string PatientName,
    string DoctorName,
    string? Diagnosis,
    VisitStatus VisitStatus,
    PrescriptionStatus? PrescriptionStatus);

public sealed record PrescriptionDto(
    int PrescriptionId,
    int PatientId,
    PrescriptionStatus Status,
    string? ReturnReason);
