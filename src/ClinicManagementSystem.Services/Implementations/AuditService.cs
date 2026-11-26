using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

/// <summary>Chỉ ĐỌC nhật ký. Cố ý không có hàm sửa/xóa: nhật ký là bằng chứng, không ai được chỉnh.</summary>
public sealed class AuditService(ClinicDbContext db, PermissionGuard guard) : IAuditService
{
    private const int PageSize = 25;

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditSearchDto search, CancellationToken ct = default)
    {
        guard.Require("xem nhật ký hệ thống", UserRole.Admin);

        var query = db.AuditLogs.AsNoTracking();
        var text = search.Keyword?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            query = query.Where(x => x.Action.Contains(text) || x.ActorNameSnapshot.Contains(text) || x.EntityId == text);
        }

        if (!string.IsNullOrWhiteSpace(search.EntityName))
        {
            query = query.Where(x => x.EntityName == search.EntityName);
        }

        if (search.From is { } from)
        {
            var fromUtc = ClinicClock.DayRangeUtc(from).StartUtc;
            query = query.Where(x => x.OccurredAtUtc >= fromUtc);
        }

        if (search.To is { } to)
        {
            var toUtc = ClinicClock.DayRangeUtc(to).EndUtc;
            query = query.Where(x => x.OccurredAtUtc < toUtc);
        }

        // Phân trang: đếm tổng, rồi chỉ lấy đúng một trang (OFFSET ... FETCH trong SQL).
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, search.Page);
        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.AuditLogId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new AuditLogDto(
                x.AuditLogId, x.OccurredAtUtc, x.ActorNameSnapshot, x.Action, x.EntityName, x.EntityId,
                x.BeforeJson, x.AfterJson))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto>(items, page, PageSize, total);
    }
}
