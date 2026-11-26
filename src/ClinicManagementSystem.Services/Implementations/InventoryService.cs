using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class InventoryService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IInventoryService
{
    public async Task<IReadOnlyList<MedicineDto>> GetCatalogAsync(bool includeInactive, CancellationToken ct = default)
    {
        guard.Require("xem kho thuốc", UserRole.Admin, UserRole.Pharmacist, UserRole.Doctor);
        return await ProjectToDto(db.Medicines.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Name))
            .ToListAsync(ct);
    }

    public async Task<MedicineDto?> GetAsync(int medicineId, CancellationToken ct = default)
    {
        guard.Require("xem thuốc", UserRole.Admin, UserRole.Pharmacist);
        return await ProjectToDto(db.Medicines.AsNoTracking().Where(x => x.MedicineId == medicineId)).SingleOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<int>> SaveMedicineAsync(SaveMedicineDto dto, CancellationToken ct = default)
    {
        guard.Require("quản lý danh mục thuốc", UserRole.Admin, UserRole.Pharmacist);

        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (dto.Name ?? string.Empty).Trim();
        var ingredient = (dto.ActiveIngredient ?? string.Empty).Trim();
        if (code.Length is 0 or > 40 || name.Length is 0 or > 180 || ingredient.Length is 0 or > 240
            || string.IsNullOrWhiteSpace(dto.Unit) || dto.UnitPrice < 0 || dto.ReorderLevel < 0)
        {
            return ServiceResult<int>.Failure("MEDICINE_INVALID",
                "Mã, tên, hoạt chất, đơn vị là bắt buộc; đơn giá và ngưỡng cảnh báo không được âm.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        Medicine medicine;
        object? before = null;
        if (dto.MedicineId is null)
        {
            // Thuốc mới luôn có tồn = 0. Muốn có hàng phải "nhập kho" để có dòng sổ kho tương ứng.
            medicine = new Medicine { StockQuantity = 0 };
            db.Medicines.Add(medicine);
        }
        else
        {
            var existing = await db.Medicines.SingleOrDefaultAsync(x => x.MedicineId == dto.MedicineId, ct);
            if (existing is null)
            {
                return ServiceResult<int>.Failure("MEDICINE_NOT_FOUND", "Không tìm thấy thuốc.");
            }

            medicine = existing;
            before = new { medicine.Code, medicine.Name, medicine.UnitPrice, medicine.ReorderLevel, medicine.IsActive };
        }

        medicine.Code = code;
        medicine.Name = name;
        medicine.ActiveIngredient = ingredient;
        // Khóa chuẩn hóa dùng để so khớp dị ứng - phải cùng quy tắc với Allergy.AllergenKey.
        medicine.ActiveIngredientKey = CodeFormatter.NormalizeKey(ingredient);
        medicine.TherapeuticGroupKey = string.IsNullOrWhiteSpace(dto.TherapeuticGroup)
            ? null
            : CodeFormatter.NormalizeKey(dto.TherapeuticGroup);
        medicine.Strength = (dto.Strength ?? string.Empty).Trim();
        medicine.DosageForm = (dto.DosageForm ?? string.Empty).Trim();
        medicine.Unit = dto.Unit.Trim();
        medicine.UnitPrice = dto.UnitPrice;
        medicine.ReorderLevel = dto.ReorderLevel;
        medicine.IsActive = dto.IsActive;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<int>.Failure("MEDICINE_CHANGED", "Thuốc vừa được người khác cập nhật. Vui lòng tải lại.");
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<int>.Failure("MEDICINE_CODE_TAKEN", "Mã thuốc này đã tồn tại.");
        }

        AuditLogWriter.Add(db, currentUser, before is null ? "Medicine.Created" : "Medicine.Updated",
            nameof(Medicine), medicine.MedicineId, before,
            new { medicine.Code, medicine.Name, medicine.UnitPrice, medicine.ReorderLevel, medicine.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<int>.Success(medicine.MedicineId, "Đã lưu thông tin thuốc.");
    }

    /// <summary>Nhập kho / điều chỉnh tồn: đổi số tồn VÀ ghi sổ kho trong cùng một transaction.</summary>
    public async Task<ServiceResult> AdjustStockAsync(StockAdjustmentDto dto, CancellationToken ct = default)
    {
        guard.Require("điều chỉnh tồn kho", UserRole.Admin, UserRole.Pharmacist);

        var reason = dto.Reason?.Trim();
        if (dto.QuantityChange == 0 || Math.Abs(dto.QuantityChange) > 100_000 || string.IsNullOrEmpty(reason) || reason.Length > 250)
        {
            return ServiceResult.Failure("ADJUSTMENT_INVALID",
                "Số lượng điều chỉnh phải khác 0 (tối đa 100.000) và bắt buộc có lý do (tối đa 250 ký tự).");
        }

        var medicine = await db.Medicines.SingleOrDefaultAsync(x => x.MedicineId == dto.MedicineId, ct);
        if (medicine is null)
        {
            return ServiceResult.Failure("MEDICINE_NOT_FOUND", "Không tìm thấy thuốc.");
        }

        var newStock = medicine.StockQuantity + dto.QuantityChange;
        if (newStock < 0)
        {
            return ServiceResult.Failure("STOCK_NEGATIVE",
                $"Không thể giảm quá tồn hiện tại ({medicine.StockQuantity:0.###} {medicine.Unit}).");
        }

        var previousStock = medicine.StockQuantity;
        medicine.StockQuantity = newStock;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            MedicineId = medicine.MedicineId,
            QuantityChange = dto.QuantityChange,
            Type = InventoryTransactionType.ManualAdjustment,
            Reference = reason,
            OccurredAtUtc = DateTime.UtcNow,
            PerformedByUserId = currentUser.UserId!.Value
        });
        AuditLogWriter.Add(db, currentUser, "Medicine.StockAdjusted", nameof(Medicine), medicine.MedicineId,
            new { StockQuantity = previousStock }, new { medicine.StockQuantity, dto.QuantityChange, Reason = reason });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Failure("STOCK_CHANGED_RELOAD",
                "Tồn kho vừa thay đổi bởi giao dịch khác (ví dụ một lần cấp phát). Vui lòng tải lại rồi thử lại.");
        }

        return ServiceResult.Success($"Đã cập nhật tồn kho {medicine.Name}: {previousStock:0.###} → {newStock:0.###} {medicine.Unit}.");
    }

    public async Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(int? medicineId, CancellationToken ct = default)
    {
        guard.Require("xem sổ kho", UserRole.Admin, UserRole.Pharmacist);

        var query =
            from t in db.InventoryTransactions.AsNoTracking()
            join u in db.Users.AsNoTracking() on t.PerformedByUserId equals u.UserId
            where medicineId == null || t.MedicineId == medicineId
            orderby t.OccurredAtUtc descending, t.InventoryTransactionId descending
            select new InventoryTransactionDto(
                t.InventoryTransactionId, t.OccurredAtUtc, t.Medicine.Name, t.Medicine.Unit,
                t.QuantityChange, t.Type, t.Reference, u.DisplayName);

        return await query.Take(200).ToListAsync(ct);
    }

    private static IQueryable<MedicineDto> ProjectToDto(IQueryable<Medicine> query)
        => query.Select(x => new MedicineDto(
            x.MedicineId, x.Code, x.Name, x.ActiveIngredient, x.TherapeuticGroupKey, x.Strength, x.DosageForm,
            x.Unit, x.UnitPrice, x.StockQuantity, x.ReorderLevel, x.IsActive));
}
