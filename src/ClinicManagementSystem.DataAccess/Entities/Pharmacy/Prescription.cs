using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.DataAccess.Entities.Clinical;

namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

public sealed class Prescription
{
    public int PrescriptionId { get; set; }
    public int VisitId { get; set; }
    public int CreatedByDoctorId { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Draft;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public string? ReturnReason { get; set; }
    public int? ReturnedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Visit Visit { get; set; } = null!;
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public ICollection<PrescriptionStatusHistory> StatusHistory { get; set; } = new List<PrescriptionStatusHistory>();
}
