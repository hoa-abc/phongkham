namespace ClinicManagementSystem.DataAccess.Entities.Billing;

/// <summary>Danh mục dịch vụ và giá (bảng ServiceCatalog).</summary>
public sealed class ClinicService
{
    public int ServiceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
