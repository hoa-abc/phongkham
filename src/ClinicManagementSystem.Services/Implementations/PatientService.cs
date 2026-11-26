using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Patients;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class PatientService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IPatientService
{
    private const int MaxResults = 100;
    private const int MaxPediatricAgeYears = 18;

    // ------------------------------------------------------------------ BỆNH NHI

    public async Task<IReadOnlyList<PatientListItemDto>> SearchAsync(string? keyword, CancellationToken ct = default)
    {
        guard.RequireSignedIn("tra cứu hồ sơ bệnh nhi");

        // IQueryable: câu truy vấn CHƯA chạy. Ta ghép dần điều kiện, đến ToListAsync mới gửi 1 câu SQL duy nhất.
        var query = db.Patients.AsNoTracking().Where(x => x.IsActive);
        var text = keyword?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var digits = CodeFormatter.NormalizePhone(text);
            query = query.Where(x =>
                x.FullName.Contains(text)
                || x.MedicalRecordNumber.Contains(text)
                || x.Guardian.FullName.Contains(text)
                || (digits.Length >= 3 && x.Guardian.PhoneNumber.Contains(digits)));
        }

        return await query
            .OrderByDescending(x => x.PatientId)
            .Take(MaxResults)
            .Select(x => new PatientListItemDto(
                x.PatientId, x.MedicalRecordNumber, x.FullName, x.DateOfBirth, x.Gender,
                x.GuardianId, x.Guardian.FullName, x.Guardian.PhoneNumber,
                x.Allergies.Where(a => a.IsActive).Select(a => a.DisplayName).ToList()))
            .ToListAsync(ct);
    }

    public async Task<PatientDetailDto?> GetAsync(int patientId, CancellationToken ct = default)
    {
        guard.RequireSignedIn("xem hồ sơ bệnh nhi");

        var patient = await db.Patients.AsNoTracking()
            .Include(x => x.Guardian)
            .Include(x => x.Allergies)
            .SingleOrDefaultAsync(x => x.PatientId == patientId, ct);
        if (patient is null)
        {
            return null;
        }

        var visits = await db.Visits.AsNoTracking()
            .Where(x => x.Appointment.PatientId == patientId)
            .OrderByDescending(x => x.Appointment.StartsAtUtc)
            .Take(20)
            .Select(x => new PatientVisitSummaryDto(
                x.VisitId, x.Appointment.StartsAtUtc, x.Appointment.Doctor.DisplayName, x.Diagnosis, x.Status))
            .ToListAsync(ct);

        return new PatientDetailDto(
            patient.PatientId, patient.MedicalRecordNumber, patient.FullName, patient.DateOfBirth, patient.Gender,
            patient.GuardianRelationship, patient.IsActive, patient.GuardianId, patient.Guardian.FullName,
            patient.Guardian.PhoneNumber, patient.Guardian.Address,
            patient.Allergies
                .OrderByDescending(a => a.IsActive).ThenBy(a => a.DisplayName)
                .Select(a => new AllergyDto(a.AllergyId, a.AllergenKey, a.DisplayName, a.Reaction, a.Severity, a.IsActive))
                .ToList(),
            visits);
    }

    public async Task<ServiceResult<int>> CreateAsync(CreatePatientDto dto, CancellationToken ct = default)
    {
        guard.Require("tạo hồ sơ bệnh nhi", UserRole.Admin, UserRole.Receptionist);

        var fullName = (dto.FullName ?? string.Empty).Trim();
        var guardianName = (dto.GuardianName ?? string.Empty).Trim();
        var phone = CodeFormatter.NormalizePhone(dto.GuardianPhone);
        var error = ValidatePatientBasics(fullName, dto.DateOfBirth)
            ?? (phone.Length is < 9 or > 11 ? "Số điện thoại người giám hộ phải có 9-11 chữ số." : null);
        if (error is not null)
        {
            return ServiceResult<int>.Failure("PATIENT_INVALID", error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Tìm phụ huynh theo số điện thoại trước khi tạo mới -> không sinh hồ sơ phụ huynh trùng.
        var guardian = await db.Guardians.SingleOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        if (guardian is null)
        {
            if (guardianName.Length == 0)
            {
                return ServiceResult<int>.Failure("GUARDIAN_NAME_REQUIRED", "Vui lòng nhập họ tên người giám hộ.");
            }

            guardian = new Guardian
            {
                FullName = guardianName,
                PhoneNumber = phone,
                Address = string.IsNullOrWhiteSpace(dto.GuardianAddress) ? null : dto.GuardianAddress.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        var patient = new Patient
        {
            Guardian = guardian,
            GuardianRelationship = dto.GuardianRelationship,
            MedicalRecordNumber = $"TMP-{Guid.NewGuid():N}", // mã tạm; mã thật cần PatientId do database sinh
            FullName = fullName,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(dto.AllergyName))
        {
            patient.Allergies.Add(BuildAllergy(dto.AllergyName, dto.AllergenKey, dto.AllergySeverity, dto.AllergyReaction));
        }

        db.Patients.Add(patient);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<int>.Failure("GUARDIAN_PHONE_TAKEN",
                "Số điện thoại vừa được dùng để tạo hồ sơ phụ huynh khác. Vui lòng thử lại.");
        }

        patient.MedicalRecordNumber = CodeFormatter.PatientCode(patient.PatientId);
        AuditLogWriter.Add(db, currentUser, "Patient.Created", nameof(Patient), patient.PatientId, null,
            new { patient.MedicalRecordNumber, patient.FullName, patient.DateOfBirth, patient.GuardianId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<int>.Success(patient.PatientId, $"Đã tạo hồ sơ {patient.MedicalRecordNumber} - {patient.FullName}.");
    }

    public async Task<ServiceResult> UpdateAsync(UpdatePatientDto dto, CancellationToken ct = default)
    {
        guard.Require("cập nhật hồ sơ bệnh nhi", UserRole.Admin, UserRole.Receptionist);

        var fullName = (dto.FullName ?? string.Empty).Trim();
        var error = ValidatePatientBasics(fullName, dto.DateOfBirth);
        if (error is not null)
        {
            return ServiceResult.Failure("PATIENT_INVALID", error);
        }

        var patient = await db.Patients.SingleOrDefaultAsync(x => x.PatientId == dto.PatientId, ct);
        if (patient is null)
        {
            return ServiceResult.Failure("PATIENT_NOT_FOUND", "Không tìm thấy hồ sơ bệnh nhi.");
        }

        var before = new { patient.FullName, patient.DateOfBirth, patient.Gender, patient.GuardianRelationship };
        patient.FullName = fullName;
        patient.DateOfBirth = dto.DateOfBirth;
        patient.Gender = dto.Gender;
        patient.GuardianRelationship = dto.GuardianRelationship;
        AuditLogWriter.Add(db, currentUser, "Patient.Updated", nameof(Patient), patient.PatientId, before,
            new { patient.FullName, patient.DateOfBirth, patient.Gender, patient.GuardianRelationship });

        await db.SaveChangesAsync(ct); // một lần SaveChanges = một transaction ngầm: hồ sơ + nhật ký cùng lưu
        return ServiceResult.Success("Đã cập nhật hồ sơ bệnh nhi.");
    }

    public async Task<ServiceResult> AddAllergyAsync(AddAllergyDto dto, CancellationToken ct = default)
    {
        guard.Require("ghi nhận dị ứng", UserRole.Admin, UserRole.Receptionist, UserRole.Doctor);

        if (string.IsNullOrWhiteSpace(dto.DisplayName) || dto.DisplayName.Trim().Length > 160)
        {
            return ServiceResult.Failure("ALLERGY_INVALID", "Vui lòng nhập tên dị nguyên (tối đa 160 ký tự).");
        }

        if (!await db.Patients.AnyAsync(x => x.PatientId == dto.PatientId, ct))
        {
            return ServiceResult.Failure("PATIENT_NOT_FOUND", "Không tìm thấy hồ sơ bệnh nhi.");
        }

        var allergy = BuildAllergy(dto.DisplayName, dto.AllergenKey, dto.Severity, dto.Reaction);
        allergy.PatientId = dto.PatientId;
        db.Allergies.Add(allergy);
        AuditLogWriter.Add(db, currentUser, "Allergy.Added", nameof(Patient), dto.PatientId, null,
            new { allergy.DisplayName, allergy.AllergenKey, allergy.Severity });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult.Failure("ALLERGY_DUPLICATE", "Dị ứng này đã được ghi nhận cho bệnh nhi.");
        }

        return ServiceResult.Success("Đã ghi nhận dị ứng.");
    }

    public async Task<ServiceResult> DeactivateAllergyAsync(int allergyId, CancellationToken ct = default)
    {
        guard.Require("ngừng hiệu lực dị ứng", UserRole.Admin, UserRole.Doctor);

        var allergy = await db.Allergies.SingleOrDefaultAsync(x => x.AllergyId == allergyId && x.IsActive, ct);
        if (allergy is null)
        {
            return ServiceResult.Failure("ALLERGY_NOT_FOUND", "Không tìm thấy dị ứng đang hiệu lực.");
        }

        allergy.IsActive = false; // xóa mềm: giữ lại lịch sử
        AuditLogWriter.Add(db, currentUser, "Allergy.Deactivated", nameof(Patient), allergy.PatientId,
            new { allergy.DisplayName, IsActive = true }, new { allergy.DisplayName, IsActive = false });
        await db.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã ngừng hiệu lực dị ứng.");
    }

    // ------------------------------------------------------------------ NGƯỜI GIÁM HỘ

    public async Task<IReadOnlyList<GuardianListItemDto>> SearchGuardiansAsync(string? keyword, CancellationToken ct = default)
    {
        guard.Require("tra cứu người giám hộ", UserRole.Admin, UserRole.Receptionist);

        var query = db.Guardians.AsNoTracking();
        var text = keyword?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var digits = CodeFormatter.NormalizePhone(text);
            query = query.Where(x => x.FullName.Contains(text) || (digits.Length >= 3 && x.PhoneNumber.Contains(digits)));
        }

        return await query
            .OrderBy(x => x.FullName)
            .Take(MaxResults)
            .Select(x => new GuardianListItemDto(x.GuardianId, x.FullName, x.PhoneNumber, x.Email, x.Address, x.Patients.Count))
            .ToListAsync(ct);
    }

    public async Task<GuardianDetailDto?> GetGuardianAsync(int guardianId, CancellationToken ct = default)
    {
        guard.Require("xem người giám hộ", UserRole.Admin, UserRole.Receptionist);

        return await db.Guardians.AsNoTracking()
            .Where(x => x.GuardianId == guardianId)
            .Select(x => new GuardianDetailDto(
                x.GuardianId, x.FullName, x.PhoneNumber, x.Email, x.Address,
                x.Patients.OrderBy(p => p.DateOfBirth).Select(p => new PatientListItemDto(
                    p.PatientId, p.MedicalRecordNumber, p.FullName, p.DateOfBirth, p.Gender,
                    x.GuardianId, x.FullName, x.PhoneNumber,
                    p.Allergies.Where(a => a.IsActive).Select(a => a.DisplayName).ToList())).ToList()))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<GuardianListItemDto?> FindGuardianByPhoneAsync(string phone, CancellationToken ct = default)
    {
        guard.Require("tra cứu người giám hộ", UserRole.Admin, UserRole.Receptionist);

        var digits = CodeFormatter.NormalizePhone(phone);
        if (digits.Length == 0)
        {
            return null;
        }

        return await db.Guardians.AsNoTracking()
            .Where(x => x.PhoneNumber == digits)
            .Select(x => new GuardianListItemDto(x.GuardianId, x.FullName, x.PhoneNumber, x.Email, x.Address, x.Patients.Count))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<int>> SaveGuardianAsync(SaveGuardianDto dto, CancellationToken ct = default)
    {
        guard.Require("lưu thông tin người giám hộ", UserRole.Admin, UserRole.Receptionist);

        var name = (dto.FullName ?? string.Empty).Trim();
        var phone = CodeFormatter.NormalizePhone(dto.PhoneNumber);
        if (name.Length is 0 or > 160 || phone.Length is < 9 or > 11)
        {
            return ServiceResult<int>.Failure("GUARDIAN_INVALID", "Họ tên là bắt buộc và số điện thoại phải có 9-11 chữ số.");
        }

        Guardian guardian;
        if (dto.GuardianId is null)
        {
            guardian = new Guardian { CreatedAtUtc = DateTime.UtcNow };
            db.Guardians.Add(guardian);
        }
        else
        {
            var existing = await db.Guardians.SingleOrDefaultAsync(x => x.GuardianId == dto.GuardianId, ct);
            if (existing is null)
            {
                return ServiceResult<int>.Failure("GUARDIAN_NOT_FOUND", "Không tìm thấy người giám hộ.");
            }

            guardian = existing;
        }

        guardian.FullName = name;
        guardian.PhoneNumber = phone;
        guardian.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        guardian.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<int>.Failure("GUARDIAN_PHONE_TAKEN", "Số điện thoại này đã thuộc về một người giám hộ khác.");
        }

        return ServiceResult<int>.Success(guardian.GuardianId, "Đã lưu thông tin người giám hộ.");
    }

    // ------------------------------------------------------------------ HÀM PHỤ

    private static string? ValidatePatientBasics(string fullName, DateOnly dateOfBirth)
    {
        if (fullName.Length is 0 or > 160)
        {
            return "Vui lòng nhập họ tên bé (tối đa 160 ký tự).";
        }

        var today = ClinicClock.Today;
        if (dateOfBirth > today)
        {
            return "Ngày sinh không được ở tương lai.";
        }

        if (dateOfBirth < today.AddYears(-MaxPediatricAgeYears))
        {
            return $"Phòng khám nhi chỉ nhận bệnh nhi dưới {MaxPediatricAgeYears} tuổi.";
        }

        return null;
    }

    private static Allergy BuildAllergy(string displayName, string? allergenKey, AllergySeverity severity, string? reaction)
    {
        var name = displayName.Trim();
        return new Allergy
        {
            DisplayName = name,
            // Không nhập khóa riêng thì lấy luôn tên dị nguyên làm khóa (đã chuẩn hóa).
            AllergenKey = CodeFormatter.NormalizeKey(string.IsNullOrWhiteSpace(allergenKey) ? name : allergenKey),
            Severity = severity,
            Reaction = string.IsNullOrWhiteSpace(reaction) ? null : reaction.Trim(),
            IsActive = true,
            RecordedAtUtc = DateTime.UtcNow
        };
    }
}
