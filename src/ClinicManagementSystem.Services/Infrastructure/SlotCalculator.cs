using ClinicManagementSystem.Common.DTOs;

namespace ClinicManagementSystem.Services.Infrastructure;

/// <summary>
/// Sinh các khung giờ khám CỐ ĐỊNH 30 phút trong ngày: sáng 08:00-11:30, chiều 13:30-17:00.
/// Dùng khung cố định (thay vì giờ tự do) làm bài toán chống trùng lịch đơn giản và chắc chắn hơn.
/// Hàm thuần (pure function): không đụng database, nên test rất dễ.
/// </summary>
internal static class SlotCalculator
{
    public const int SlotMinutes = 30;

    private static readonly (TimeOnly From, TimeOnly To)[] Sessions =
    [
        (new TimeOnly(8, 0), new TimeOnly(11, 30)),
        (new TimeOnly(13, 30), new TimeOnly(17, 0))
    ];

    /// <param name="date">Ngày khám theo giờ phòng khám.</param>
    /// <param name="busyLocal">Các khoảng giờ bác sĩ đã có lịch (giờ phòng khám).</param>
    /// <param name="nowLocal">Giờ hiện tại, để khóa các khung đã qua.</param>
    public static IReadOnlyList<SlotDto> Build(
        DateOnly date,
        IReadOnlyCollection<(DateTime Start, DateTime End)> busyLocal,
        DateTime nowLocal)
    {
        var slots = new List<SlotDto>();
        foreach (var (from, to) in Sessions)
        {
            for (var time = from; time < to; time = time.AddMinutes(SlotMinutes))
            {
                var start = date.ToDateTime(time);
                var end = start.AddMinutes(SlotMinutes);

                string? reason = null;
                if (start <= nowLocal)
                {
                    reason = "Đã qua giờ";
                }
                else if (busyLocal.Any(busy => busy.Start < end && busy.End > start)) // hai khoảng giao nhau
                {
                    reason = "Đã đặt";
                }

                slots.Add(new SlotDto(start, end, reason is null, reason));
            }
        }

        return slots;
    }

    /// <summary>Giờ bắt đầu có đúng là một khung hợp lệ của phòng khám không?</summary>
    public static bool IsValidSlotStart(DateTime startLocal)
    {
        var time = TimeOnly.FromDateTime(startLocal);
        return startLocal.Second == 0
            && startLocal.Minute % SlotMinutes == 0
            && Sessions.Any(session => time >= session.From && time < session.To);
    }
}
