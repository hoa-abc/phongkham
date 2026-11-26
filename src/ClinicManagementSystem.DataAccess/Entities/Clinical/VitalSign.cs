namespace ClinicManagementSystem.DataAccess.Entities.Clinical;

/// <summary>Sinh hiệu đo tại quầy khi check-in. Cho phép nhiều lần đo trong một lượt khám.</summary>
public sealed class VitalSign
{
    public int VitalSignId { get; set; }
    public int VisitId { get; set; }
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public decimal TemperatureC { get; set; }
    public int HeartRate { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public int RecordedByUserId { get; set; }
}
