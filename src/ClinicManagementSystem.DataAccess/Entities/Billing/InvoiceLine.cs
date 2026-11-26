namespace ClinicManagementSystem.DataAccess.Entities.Billing;

/// <summary>Một dòng trên hóa đơn. Mô tả và đơn giá được "chụp" lại, không tham chiếu ngược về bảng giá.</summary>
public sealed class InvoiceLine
{
    public int InvoiceLineId { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitLabel { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
