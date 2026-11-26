using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.Security;

/// <summary>
/// "Ai đang thao tác?" - tầng Services chỉ biết interface này, không biết gì về HTTP hay cookie.
/// Tầng Web cung cấp bản cài đặt đọc từ cookie đăng nhập (HttpCurrentUserContext);
/// project test cung cấp bản giả (FakeCurrentUser). Nhờ vậy Services test được mà không cần trình duyệt.
/// </summary>
public interface ICurrentUserContext
{
    int? UserId { get; }
    UserRole? Role { get; }
    string? DisplayName { get; }
    /// <summary>Mã nối các dòng nhật ký thuộc cùng một request.</summary>
    Guid? CorrelationId { get; }
}
