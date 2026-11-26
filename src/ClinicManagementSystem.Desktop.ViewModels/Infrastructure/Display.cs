using System.Globalization;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.Common.Time;

namespace ClinicManagementSystem.Desktop.ViewModels.Infrastructure;

/// <summary>
/// Định dạng hiển thị kiểu Việt Nam (giờ VN, 150.000 ₫, 0901 234 567). Giống lớp Fmt bên web.
/// Đặt ở tầng ViewModel để màn hình chỉ việc hiển thị chuỗi có sẵn, và để test kiểm tra được.
/// </summary>
public static class Display
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Time(DateTime utc) => ClinicClock.ToLocal(utc).ToString("HH:mm", Vi);
    public static string Date(DateTime utc) => ClinicClock.ToLocal(utc).ToString("dd/MM/yyyy", Vi);
    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", Vi);
    public static string DateTimeText(DateTime utc) => ClinicClock.ToLocal(utc).ToString("HH:mm dd/MM/yyyy", Vi);
    public static string Money(decimal amount) => amount.ToString("N0", Vi) + " ₫";
    public static string Qty(decimal quantity) => quantity.ToString("0.###", Vi);
    public static string Age(DateOnly dateOfBirth) => PediatricAgeFormatter.Format(dateOfBirth, ClinicClock.Today);

    public static string Phone(string? digits)
        => digits is { Length: 10 } ? $"{digits[..4]} {digits[4..7]} {digits[7..]}" : digits ?? string.Empty;
}

/// <summary>Một lựa chọn trong ô thả xuống (ComboBox): giá trị + chữ hiển thị.</summary>
public sealed record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// "Dải nhận diện bệnh nhi" ở đầu các màn hình quan trọng (check-in, khám, thu tiền, cấp thuốc):
/// luôn thấy tên, mã, tuổi, phụ huynh và DỊ ỨNG để không làm nhầm bé.
/// </summary>
public sealed record PatientBanner(
    string FullName,
    string MedicalRecordNumber,
    string AgeAndGender,
    string Guardian,
    string Vitals,
    string Allergies,
    bool HasAllergies)
{
    public static PatientBanner From(PatientContextDto p)
    {
        var vitals = p.WeightKg is null
            ? "Chưa có sinh hiệu"
            : $"{Display.Qty(p.WeightKg.Value)} kg · {Display.Qty(p.HeightCm ?? 0)} cm · " +
              $"{Display.Qty(p.TemperatureC ?? 0)} °C · {p.HeartRate} lần/phút";
        return new PatientBanner(
            p.FullName,
            p.MedicalRecordNumber,
            $"{Display.Age(p.DateOfBirth)} · {p.Gender.ToLabel()} · Sinh {Display.Date(p.DateOfBirth)}",
            $"{p.GuardianName} · {Display.Phone(p.GuardianPhone)}",
            vitals,
            p.Allergies.Count > 0 ? "Dị ứng: " + string.Join(", ", p.Allergies) : "Không ghi nhận dị ứng",
            p.Allergies.Count > 0);
    }
}
