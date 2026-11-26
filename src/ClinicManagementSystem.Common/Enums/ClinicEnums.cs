namespace ClinicManagementSystem.Common.Enums;

// Enum = tập giá trị cố định có tên. Dùng enum thay cho chuỗi/số "ma thuật" để
// trình biên dịch bắt lỗi gõ sai. Trong database, enum được lưu dưới dạng CHUỖI
// (xem ClinicDbContext: HasConversion<string>()) để đọc bảng dễ hiểu hơn.

/// <summary>Bốn vai trò người dùng của phòng khám.</summary>
public enum UserRole
{
    Admin = 1,
    Receptionist = 2,
    Doctor = 3,
    Pharmacist = 4
}

/// <summary>Vòng đời của một LỊCH HẸN.</summary>
public enum AppointmentStatus
{
    Booked = 1,          // Đã đặt
    CheckedIn = 2,       // Đã check-in tại quầy
    InConsultation = 3,  // Bác sĩ đang khám
    Completed = 4,       // Bác sĩ đã khám xong
    Cancelled = 5,       // Đã hủy (giải phóng khung giờ)
    NoShow = 6           // Không đến
}

/// <summary>Vòng đời của một LƯỢT KHÁM (visit) - đi xuyên suốt từ quầy tiếp tân tới quầy thuốc.</summary>
public enum VisitStatus
{
    Waiting = 1,          // Đã check-in, chờ bác sĩ
    InConsultation = 2,   // Đang khám (hoặc đơn bị trả về bác sĩ)
    AwaitingPayment = 3,  // Khám xong, chờ thu tiền
    AwaitingDispense = 4, // Đã thu tiền, chờ cấp thuốc
    Completed = 5,        // Hoàn tất
    Cancelled = 6
}

public enum PrescriptionStatus
{
    Draft = 1,             // Bác sĩ đang soạn
    Issued = 2,            // Đã phát hành, chờ thanh toán
    ReturnedToDoctor = 3,  // Dược sĩ trả về kèm lý do
    ReadyForDispense = 4,  // Đã thanh toán, đủ điều kiện cấp phát
    Dispensed = 5,         // Đã cấp phát
    Cancelled = 6
}

public enum InvoiceStatus
{
    Unpaid = 1,
    Paid = 2,
    Voided = 3   // Đã hủy/hoàn tiền bằng nghiệp vụ điều chỉnh
}

public enum PaymentStatus
{
    Succeeded = 1,
    Failed = 2,
    Reversed = 3 // Đã hoàn lại (khi hóa đơn bị hủy)
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3
}

public enum DispenseStatus
{
    Confirmed = 1,
    Reversed = 2
}

/// <summary>Loại giao dịch kho. Mỗi lần tồn kho thay đổi đều có một dòng "sổ kho".</summary>
public enum InventoryTransactionType
{
    OpeningBalance = 1,   // Tồn đầu kỳ
    Dispense = 2,         // Xuất do cấp phát
    ManualAdjustment = 3, // Nhập/điều chỉnh tay
    Reversal = 4
}

public enum WarningSeverity
{
    Red = 1,    // Dị ứng
    Orange = 2  // Thiếu tồn kho
}

public enum WarningCode
{
    AllergyMatch = 1,
    StockInsufficient = 2
}

public enum AllergySeverity
{
    Unknown = 0,
    Mild = 1,
    Moderate = 2,
    Severe = 3
}

public enum Gender
{
    Unknown = 0,
    Female = 1,
    Male = 2,
    Other = 3
}

public enum GuardianRelationship
{
    Parent = 1,
    Grandparent = 2,
    Other = 3
}
