using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Infrastructure;

/// <summary>
/// Dịch mã lỗi của SQL Server thành ý nghĩa nghiệp vụ.
///  - 2601 / 2627: vi phạm unique index / unique constraint (ví dụ: hai người đặt cùng khung giờ).
///  - 1205: transaction bị chọn làm "nạn nhân deadlock" và đã bị SQL Server hủy.
/// </summary>
internal static class SqlServerErrorDetector
{
    public static bool IsUniqueConstraintViolation(Exception exception) => HasSqlError(exception, 2601, 2627);

    public static bool IsDeadlock(Exception exception) => HasSqlError(exception, 1205);

    private static bool HasSqlError(Exception exception, params int[] numbers)
    {
        // Lỗi gốc thường nằm sâu trong InnerException nên phải lần theo chuỗi.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && numbers.Contains(sqlException.Number))
            {
                return true;
            }
        }

        return false;
    }
}
