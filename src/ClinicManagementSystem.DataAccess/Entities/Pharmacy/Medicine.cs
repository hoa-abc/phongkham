namespace ClinicManagementSystem.DataAccess.Entities.Pharmacy;

public sealed class Medicine
{
    public int MedicineId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ActiveIngredient { get; set; } = string.Empty;
    /// <summary>Khóa hoạt chất đã chuẩn hóa, ví dụ PARACETAMOL.</summary>
    public string ActiveIngredientKey { get; set; } = string.Empty;
    /// <summary>Khóa nhóm thuốc/nhóm dị ứng đã chuẩn hóa, ví dụ PENICILLIN.</summary>
    public string? TherapeuticGroupKey { get; set; }
    public string Strength { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal StockQuantity { get; set; }
    /// <summary>Ngưỡng cảnh báo sắp hết hàng.</summary>
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Chống hai dược sĩ cùng trừ kho một lúc (optimistic concurrency).</summary>
    public byte[] RowVersion { get; set; } = [];
}
