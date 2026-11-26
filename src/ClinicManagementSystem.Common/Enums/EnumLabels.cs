namespace ClinicManagementSystem.Common.Enums;

/// <summary>
/// Chuyển enum sang nhãn tiếng Việt để hiển thị. Đặt ở Common để cả tầng Services (thông báo lỗi)
/// lẫn tầng Web (giao diện) dùng chung một cách gọi tên, tránh mỗi nơi dịch một kiểu.
/// </summary>
public static class EnumLabels
{
    public static string ToLabel(this UserRole role) => role switch
    {
        UserRole.Admin => "Quản trị viên",
        UserRole.Receptionist => "Tiếp tân",
        UserRole.Doctor => "Bác sĩ",
        UserRole.Pharmacist => "Dược sĩ",
        _ => "Không xác định"
    };

    public static string ToLabel(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Booked => "Đã đặt",
        AppointmentStatus.CheckedIn => "Đã check-in",
        AppointmentStatus.InConsultation => "Đang khám",
        AppointmentStatus.Completed => "Đã khám xong",
        AppointmentStatus.Cancelled => "Đã hủy",
        AppointmentStatus.NoShow => "Không đến",
        _ => "Chưa xác định"
    };

    public static string ToLabel(this VisitStatus status) => status switch
    {
        VisitStatus.Waiting => "Chờ khám",
        VisitStatus.InConsultation => "Đang khám",
        VisitStatus.AwaitingPayment => "Chờ thanh toán",
        VisitStatus.AwaitingDispense => "Chờ cấp thuốc",
        VisitStatus.Completed => "Hoàn tất",
        VisitStatus.Cancelled => "Đã hủy",
        _ => "Chưa xác định"
    };

    public static string ToLabel(this PrescriptionStatus status) => status switch
    {
        PrescriptionStatus.Draft => "Đơn nháp",
        PrescriptionStatus.Issued => "Chờ thanh toán",
        PrescriptionStatus.ReturnedToDoctor => "Trả về bác sĩ",
        PrescriptionStatus.ReadyForDispense => "Chờ cấp thuốc",
        PrescriptionStatus.Dispensed => "Đã cấp phát",
        PrescriptionStatus.Cancelled => "Đã hủy",
        _ => "Chưa xác định"
    };

    public static string ToLabel(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Unpaid => "Chưa thanh toán",
        InvoiceStatus.Paid => "Đã thanh toán",
        InvoiceStatus.Voided => "Đã hủy / hoàn tiền",
        _ => "Chưa xác định"
    };

    public static string ToLabel(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.Card => "Thẻ",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        _ => "Khác"
    };

    public static string ToLabel(this Gender gender) => gender switch
    {
        Gender.Male => "Nam",
        Gender.Female => "Nữ",
        Gender.Other => "Khác",
        _ => "Chưa rõ"
    };

    public static string ToLabel(this GuardianRelationship relationship) => relationship switch
    {
        GuardianRelationship.Parent => "Cha / Mẹ",
        GuardianRelationship.Grandparent => "Ông / Bà",
        _ => "Người giám hộ khác"
    };

    public static string ToLabel(this AllergySeverity severity) => severity switch
    {
        AllergySeverity.Mild => "Nhẹ",
        AllergySeverity.Moderate => "Trung bình",
        AllergySeverity.Severe => "Nặng",
        _ => "Chưa xác định"
    };

    public static string ToLabel(this InventoryTransactionType type) => type switch
    {
        InventoryTransactionType.OpeningBalance => "Tồn đầu kỳ",
        InventoryTransactionType.Dispense => "Xuất cấp phát",
        InventoryTransactionType.ManualAdjustment => "Nhập / điều chỉnh",
        InventoryTransactionType.Reversal => "Hoàn kho",
        _ => "Khác"
    };
}
