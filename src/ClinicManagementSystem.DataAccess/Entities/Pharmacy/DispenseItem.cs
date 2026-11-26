namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

public sealed class DispenseItem
{
    public int DispenseItemId { get; set; }
    public int DispenseId { get; set; }
    public int PrescriptionItemId { get; set; }
    public int MedicineId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    public Dispense Dispense { get; set; } = null!;
}
