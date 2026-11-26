using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

public sealed record CreateAppointmentDto(
    int PatientId,
    int DoctorId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason);

public sealed record AppointmentDto(
    int AppointmentId,
    int PatientId,
    int DoctorId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    AppointmentStatus Status);

public sealed record AppointmentListItemDto(
    int AppointmentId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    AppointmentStatus Status,
    int PatientId,
    string MedicalRecordNumber,
    string PatientName,
    string GuardianName,
    string GuardianPhone,
    int DoctorId,
    string DoctorName,
    string? DoctorSpecialty,
    string? Reason);

public sealed record AppointmentFilterDto(DateOnly? Date, AppointmentStatus? Status, string? Keyword);

/// <summary>Một khung giờ khám 30 phút của bác sĩ trong ngày.</summary>
public sealed record SlotDto(DateTime StartLocal, DateTime EndLocal, bool IsAvailable, string? UnavailableReason);

public sealed record CheckInDto(
    int AppointmentId,
    decimal WeightKg,
    decimal HeightCm,
    decimal TemperatureC,
    int HeartRate,
    string? Symptoms);

public sealed record CheckInContextDto(AppointmentListItemDto Appointment, PatientContextDto Patient);
