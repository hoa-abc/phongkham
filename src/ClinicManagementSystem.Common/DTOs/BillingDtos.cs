using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

public sealed record InvoiceListItemDto(
    int InvoiceId,
    string InvoiceNumber,
    string PatientName,
    string MedicalRecordNumber,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    bool NeedsAdjustment);

public sealed record InvoiceLineDto(
    string Description,
    decimal Quantity,
    string? UnitLabel,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record InvoiceDetailDto(
    int InvoiceId,
    string InvoiceNumber,
    InvoiceStatus Status,
    bool IsLocked,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    PaymentMethod? PaymentMethod,
    string? VoidReason,
    PatientContextDto Patient,
    string DoctorName,
    IReadOnlyList<InvoiceLineDto> Lines,
    bool CanPay,
    string? CannotPayReason,
    bool CanVoid);

public sealed record PaymentDto(
    int PaymentId,
    int InvoiceId,
    decimal Amount,
    PaymentMethod Method,
    PaymentStatus Status,
    DateTime PaidAtUtc);

public sealed record ClinicServiceDto(int ServiceId, string Code, string Name, decimal Price, bool IsActive);

public sealed record SaveClinicServiceDto(int? ServiceId, string Code, string Name, decimal Price, bool IsActive);
