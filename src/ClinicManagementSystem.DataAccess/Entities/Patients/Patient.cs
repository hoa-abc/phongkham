using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.DataAccess.Entities.Patients;

public sealed class Patient
{
    public int PatientId { get; set; }
    /// <summary>Khóa ngoại bắt buộc tới người giám hộ.</summary>
    public int GuardianId { get; set; }
    public GuardianRelationship GuardianRelationship { get; set; }
    /// <summary>Mã bệnh nhi hiển thị (BN-00012). Ổn định, không phụ thuộc tên.</summary>
    public string MedicalRecordNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    public Guardian Guardian { get; set; } = null!;
    public ICollection<Allergy> Allergies { get; set; } = new List<Allergy>();
}
