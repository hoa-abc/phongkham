using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Infrastructure;

/// <summary>
/// Nạp "dải nhận diện bệnh nhi" (mã, tên, tuổi, phụ huynh, sinh hiệu gần nhất, dị ứng).
/// Nhiều service cùng cần nên gom về một chỗ thay vì chép truy vấn ra nhiều nơi.
/// </summary>
internal static class PatientContextLoader
{
    public static async Task<PatientContextDto?> LoadAsync(ClinicDbContext db, int patientId, CancellationToken ct)
    {
        // AsNoTracking: chỉ đọc để hiển thị, không cần Change Tracker theo dõi -> nhanh và nhẹ hơn.
        var patient = await db.Patients.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .Select(x => new
            {
                x.PatientId,
                x.MedicalRecordNumber,
                x.FullName,
                x.DateOfBirth,
                x.Gender,
                GuardianName = x.Guardian.FullName,
                GuardianPhone = x.Guardian.PhoneNumber,
                Allergies = x.Allergies.Where(a => a.IsActive).Select(a => a.DisplayName).ToList()
            })
            .SingleOrDefaultAsync(ct);

        if (patient is null)
        {
            return null;
        }

        // Sinh hiệu gần nhất: lần theo VitalSign -> Visit -> Appointment của đúng bệnh nhi này.
        var vital = await (
                from v in db.VitalSigns.AsNoTracking()
                join visit in db.Visits.AsNoTracking() on v.VisitId equals visit.VisitId
                where visit.Appointment.PatientId == patientId
                orderby v.RecordedAtUtc descending
                select new { v.WeightKg, v.HeightCm, v.TemperatureC, v.HeartRate })
            .FirstOrDefaultAsync(ct);

        return new PatientContextDto(
            patient.PatientId,
            patient.MedicalRecordNumber,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender,
            patient.GuardianName,
            patient.GuardianPhone,
            vital?.WeightKg,
            vital?.HeightCm,
            vital?.TemperatureC,
            vital?.HeartRate,
            patient.Allergies);
    }
}
