using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class ServiceCatalogService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IServiceCatalogService
{
    public async Task<IReadOnlyList<ClinicServiceDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        guard.RequireSignedIn("xem danh mục dịch vụ");
        return await db.ClinicServices.AsNoTracking()
            .Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new ClinicServiceDto(x.ServiceId, x.Code, x.Name, x.Price, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ClinicServiceDto?> GetAsync(int serviceId, CancellationToken ct = default)
    {
        guard.Require("xem dịch vụ", UserRole.Admin);
        return await db.ClinicServices.AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .Select(x => new ClinicServiceDto(x.ServiceId, x.Code, x.Name, x.Price, x.IsActive))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<int>> SaveAsync(SaveClinicServiceDto dto, CancellationToken ct = default)
    {
        guard.Require("quản lý danh mục dịch vụ", UserRole.Admin);

        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (dto.Name ?? string.Empty).Trim();
        if (code.Length is 0 or > 40 || name.Length is 0 or > 180 || dto.Price < 0 || dto.Price > 100_000_000)
        {
            return ServiceResult<int>.Failure("SERVICE_INVALID", "Mã, tên dịch vụ là bắt buộc và giá phải từ 0 đến 100.000.000 ₫.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        ClinicService service;
        object? before = null;
        if (dto.ServiceId is null)
        {
            service = new ClinicService();
            db.ClinicServices.Add(service);
        }
        else
        {
            var existing = await db.ClinicServices.SingleOrDefaultAsync(x => x.ServiceId == dto.ServiceId, ct);
            if (existing is null)
            {
                return ServiceResult<int>.Failure("SERVICE_NOT_FOUND", "Không tìm thấy dịch vụ.");
            }

            service = existing;
            before = new { service.Code, service.Name, service.Price, service.IsActive };
        }

        service.Code = code;
        service.Name = name;
        service.Price = dto.Price; // đổi giá không ảnh hưởng hóa đơn cũ vì hóa đơn đã "chụp" giá
        service.IsActive = dto.IsActive;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<int>.Failure("SERVICE_CODE_TAKEN", "Mã dịch vụ này đã tồn tại.");
        }

        AuditLogWriter.Add(db, currentUser, before is null ? "Service.Created" : "Service.Updated",
            nameof(ClinicService), service.ServiceId, before,
            new { service.Code, service.Name, service.Price, service.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<int>.Success(service.ServiceId, "Đã lưu dịch vụ.");
    }
}
