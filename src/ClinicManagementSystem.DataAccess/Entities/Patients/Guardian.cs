namespace ClinicManagementSystem.DataAccess.Entities.Patients;

/// <summary>Phụ huynh / người giám hộ. Quan hệ 1 - N: một người giám hộ có nhiều bệnh nhi.</summary>
public sealed class Guardian
{
    public int GuardianId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Navigation property: EF Core dùng nó để JOIN sang bảng Patients.
    public ICollection<Patient> Patients { get; set; } = new List<Patient>();
}
