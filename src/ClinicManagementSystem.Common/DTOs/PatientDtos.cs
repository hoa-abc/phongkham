using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

public sealed record PatientListItemDto(
    int PatientId,
    string MedicalRecordNumber,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    int GuardianId,
    string GuardianName,
    string GuardianPhone,
    IReadOnlyList<string> Allergies);

public sealed record AllergyDto(
    int AllergyId,
    string AllergenKey,
    string DisplayName,
    string? Reaction,
    AllergySeverity Severity,
    bool IsActive);

public sealed record PatientVisitSummaryDto(
    int VisitId,
    DateTime StartsAtUtc,
    string DoctorName,
    string? Diagnosis,
    VisitStatus Status);

public sealed record PatientDetailDto(
    int PatientId,
    string MedicalRecordNumber,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    GuardianRelationship GuardianRelationship,
    bool IsActive,
    int GuardianId,
    string GuardianName,
    string GuardianPhone,
    string? GuardianAddress,
    IReadOnlyList<AllergyDto> Allergies,
    IReadOnlyList<PatientVisitSummaryDto> Visits);

/// <summary>
/// Tạo hồ sơ bệnh nhi. Người giám hộ được tìm theo số điện thoại:
/// nếu đã có thì liên kết vào hồ sơ cũ, chưa có thì tạo mới - tránh trùng hồ sơ phụ huynh.
/// </summary>
public sealed record CreatePatientDto(
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string GuardianPhone,
    string GuardianName,
    GuardianRelationship GuardianRelationship,
    string? GuardianAddress,
    string? AllergyName,
    string? AllergenKey,
    AllergySeverity AllergySeverity,
    string? AllergyReaction);

public sealed record UpdatePatientDto(
    int PatientId,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    GuardianRelationship GuardianRelationship);

public sealed record AddAllergyDto(
    int PatientId,
    string DisplayName,
    string? AllergenKey,
    AllergySeverity Severity,
    string? Reaction);

public sealed record GuardianListItemDto(
    int GuardianId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Address,
    int PatientCount);

public sealed record GuardianDetailDto(
    int GuardianId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Address,
    IReadOnlyList<PatientListItemDto> Patients);

public sealed record SaveGuardianDto(
    int? GuardianId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? Address);

/// <summary>
/// "Dải nhận diện bệnh nhi" hiển thị ở đầu các màn hình thao tác quan trọng (check-in, khám, thu tiền, cấp thuốc)
/// để nhân viên luôn chắc mình đang làm đúng hồ sơ.
/// </summary>
public sealed record PatientContextDto(
    int PatientId,
    string MedicalRecordNumber,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string GuardianName,
    string GuardianPhone,
    decimal? WeightKg,
    decimal? HeightCm,
    decimal? TemperatureC,
    int? HeartRate,
    IReadOnlyList<string> Allergies);
