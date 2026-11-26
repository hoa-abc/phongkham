namespace ClinicManagementSystem.DataAccess.Entities.Auditing;

/// <summary>Nhật ký thao tác quan trọng: ai làm gì, trên bản ghi nào, lúc nào, trước/sau ra sao.</summary>
public sealed class AuditLog
{
    public int AuditLogId { get; set; }
    public int? ActorUserId { get; set; }
    /// <summary>"Chụp" tên người thao tác: kể cả khi tài khoản bị xóa/đổi tên, nhật ký vẫn đọc được.</summary>
    public string ActorNameSnapshot { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? CorrelationId { get; set; }
}
