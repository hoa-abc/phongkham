namespace ClinicManagementSystem.Common.Formatting;

/// <summary>Quy tắc sinh mã hiển thị. Đặt một chỗ để seed, service và test dùng chung.</summary>
public static class CodeFormatter
{
    /// <summary>Mã bệnh nhi: BN-00012 (sinh từ khóa chính nên không bao giờ trùng).</summary>
    public static string PatientCode(int patientId) => $"BN-{patientId:D5}";

    /// <summary>Số hóa đơn: HD-00034, lần lập lại sau khi hủy là HD-00034-2.</summary>
    public static string InvoiceNumber(int visitId, int sequence)
        => sequence <= 1 ? $"HD-{visitId:D5}" : $"HD-{visitId:D5}-{sequence}";

    /// <summary>Chuẩn hóa khóa dị ứng/hoạt chất: bỏ khoảng trắng thừa + viết HOA để so khớp chính xác.</summary>
    public static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    /// <summary>Chỉ giữ chữ số của số điện thoại: "0901.234.567" và "0901 234 567" là một.</summary>
    public static string NormalizePhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) ? string.Empty : string.Concat(phone.Where(char.IsDigit));
}
