namespace ClinicManagementSystem.Common.Time;

/// <summary>
/// Quy ước thời gian của hệ thống: database LUÔN lưu giờ UTC, màn hình LUÔN hiển thị giờ Việt Nam.
/// Lớp này là nơi duy nhất chuyển đổi giữa hai loại giờ, để không màn hình nào tự cộng/trừ 7 tiếng.
/// </summary>
public static class ClinicClock
{
    private static readonly TimeZoneInfo ClinicZone = ResolveZone();

    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>Giờ hiện tại theo đồng hồ phòng khám (Việt Nam).</summary>
    public static DateTime LocalNow => ToLocal(DateTime.UtcNow);

    /// <summary>Ngày hôm nay theo lịch phòng khám.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public static DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ClinicZone);

    public static DateTime ToUtc(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ClinicZone);

    /// <summary>Khoảng UTC [bắt đầu, kết thúc) tương ứng với trọn một ngày theo giờ phòng khám.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly localDate)
    {
        var start = ToUtc(localDate.ToDateTime(TimeOnly.MinValue));
        return (start, start.AddDays(1));
    }

    private static TimeZoneInfo ResolveZone()
    {
        // macOS/Linux dùng tên IANA, Windows cũ dùng tên riêng -> thử lần lượt.
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Giờ Việt Nam", "Giờ Việt Nam");
    }
}
