namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

public sealed class PrescriptionItem
{
    public int PrescriptionItemId { get; set; }
    public int PrescriptionId { get; set; }
    public int MedicineId { get; set; }
    public decimal Quantity { get; set; }
    public string? DosageInstructions { get; set; }

    public Medicine Medicine { get; set; } = null!;
}
