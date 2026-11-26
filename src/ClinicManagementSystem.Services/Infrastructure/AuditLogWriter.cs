using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Auditing;

namespace ClinicManagementSystem.Services.Infrastructure;

/// <summary>
/// Thêm một dòng nhật ký vào CHÍNH DbContext của nghiệp vụ đang chạy.
/// Vì cùng DbContext nên dòng nhật ký được lưu (hoặc hủy) CÙNG LÚC với thay đổi nghiệp vụ:
/// không bao giờ có chuyện "đã thu tiền nhưng thiếu nhật ký" hay ngược lại.
/// Chỉ ghi dữ liệu tối thiểu; tuyệt đối không ghi mật khẩu hay toàn bộ bệnh án.
/// </summary>
internal static class AuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Giữ nguyên tiếng Việt có dấu và ghi enum bằng tên để nhật ký đọc được.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Add(
        ClinicDbContext db,
        ICurrentUserContext currentUser,
        string action,
        string entityName,
        int entityId,
        object? before,
        object? after)
        => Add(db, currentUser.UserId, currentUser.DisplayName ?? currentUser.Role?.ToString() ?? "Không xác định",
            currentUser.CorrelationId, action, entityName, entityId, before, after);

    public static void Add(
        ClinicDbContext db,
        int? actorUserId,
        string actorName,
        Guid? correlationId,
        string action,
        string entityName,
        int entityId,
        object? before,
        object? after)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            ActorNameSnapshot = actorName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId.ToString(CultureInfo.InvariantCulture),
            OccurredAtUtc = DateTime.UtcNow,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            CorrelationId = correlationId
        });
    }
}
