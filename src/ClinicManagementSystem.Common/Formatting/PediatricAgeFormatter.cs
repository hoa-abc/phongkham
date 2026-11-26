namespace ClinicManagementSystem.Common.Formatting;

/// <summary>Tuổi của trẻ nhỏ được nói theo THÁNG khi dưới 24 tháng, theo quy ước nhi khoa.</summary>
public static class PediatricAgeFormatter
{
    public static string Format(DateOnly dateOfBirth, DateOnly asOfDate)
    {
        var months = Math.Max(0, (asOfDate.Year - dateOfBirth.Year) * 12 + asOfDate.Month - dateOfBirth.Month);
        if (asOfDate.Day < dateOfBirth.Day)
        {
            months = Math.Max(0, months - 1); // chưa tới "ngày sinh nhật tháng" thì chưa tròn tháng
        }

        if (months < 24)
        {
            return $"{months} tháng tuổi";
        }

        var years = months / 12;
        var remainingMonths = months % 12;
        return remainingMonths == 0 ? $"{years} tuổi" : $"{years} tuổi {remainingMonths} tháng";
    }
}
