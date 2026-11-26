namespace ClinicManagementSystem.Common.Results;

/// <summary>
/// "Phong bì" kết quả mà mọi Service trả về cho Controller.
/// Thay vì ném exception cho lỗi nghiệp vụ thông thường (trùng lịch, hết thuốc...),
/// Service trả về Succeeded=false kèm mã lỗi ổn định (ErrorCode) và câu thông báo tiếng Việt (Message).
/// Controller chỉ cần kiểm tra Succeeded để quyết định hiện thông báo xanh hay đỏ.
/// </summary>
public sealed record ServiceResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode = null,
    string? Message = null)
{
    public static ServiceResult<T> Success(T value, string? message = null)
        => new(true, value, null, message);

    public static ServiceResult<T> Failure(string errorCode, string message)
        => new(false, default, errorCode, message);
}

/// <summary>Bản không mang dữ liệu, dùng cho thao tác chỉ cần biết thành công hay thất bại.</summary>
public sealed record ServiceResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? Message = null)
{
    public static ServiceResult Success(string? message = null) => new(true, null, message);

    public static ServiceResult Failure(string errorCode, string message) => new(false, errorCode, message);
}

/// <summary>Một trang dữ liệu (dùng cho màn hình nhật ký có phân trang).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
