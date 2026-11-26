using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Infrastructure;

/// <summary>
/// Tính cảnh báo cho một đơn thuốc:
///  - ĐỎ:  thuốc trùng HOẠT CHẤT hoặc NHÓM THUỐC với một dị ứng đang hiệu lực của bệnh nhi.
///  - CAM: số lượng kê lớn hơn tồn kho hiện tại.
/// Cảnh báo chỉ hỗ trợ bác sĩ rà soát; hệ thống không tự quyết định thay bác sĩ.
/// </summary>
internal static class PrescriptionWarningEvaluator
{
    public static async Task<IReadOnlyList<PrescriptionWarningDto>> EvaluateAsync(
        ClinicDbContext db,
        int patientId,
        IReadOnlyCollection<PrescriptionItemDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var allergies = await db.Allergies.AsNoTracking()
            .Where(x => x.PatientId == patientId && x.IsActive)
            .ToListAsync(ct);

        // Gộp các dòng cùng một thuốc để so với tồn kho theo TỔNG số lượng.
        var requestedByMedicine = items
            .GroupBy(x => x.MedicineId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var medicineIds = requestedByMedicine.Keys.ToList();
        var medicines = await db.Medicines.AsNoTracking()
            .Where(x => medicineIds.Contains(x.MedicineId))
            .ToDictionaryAsync(x => x.MedicineId, ct);

        var warnings = new List<PrescriptionWarningDto>();
        foreach (var (medicineId, requestedQuantity) in requestedByMedicine)
        {
            if (!medicines.TryGetValue(medicineId, out var medicine))
            {
                continue;
            }

            var ingredientKey = CodeFormatter.NormalizeKey(medicine.ActiveIngredientKey);
            var groupKey = CodeFormatter.NormalizeKey(medicine.TherapeuticGroupKey);

            foreach (var allergy in allergies)
            {
                var allergenKey = CodeFormatter.NormalizeKey(allergy.AllergenKey);
                if (allergenKey.Length == 0 || (allergenKey != ingredientKey && allergenKey != groupKey))
                {
                    continue;
                }

                var reaction = string.IsNullOrWhiteSpace(allergy.Reaction)
                    ? string.Empty
                    : $" Phản ứng từng ghi nhận: {allergy.Reaction.Trim()}.";
                warnings.Add(new PrescriptionWarningDto(
                    WarningCode.AllergyMatch,
                    WarningSeverity.Red,
                    medicine.MedicineId,
                    medicine.Name,
                    $"CẢNH BÁO DỊ ỨNG: {medicine.Name} khớp với hồ sơ dị ứng {allergy.DisplayName} " +
                    $"(mức độ {allergy.Severity.ToLabel().ToLowerInvariant()}).{reaction} Cần bác sĩ đánh giá trước khi kê.",
                    requestedQuantity,
                    medicine.StockQuantity));
            }

            if (requestedQuantity > medicine.StockQuantity)
            {
                warnings.Add(new PrescriptionWarningDto(
                    WarningCode.StockInsufficient,
                    WarningSeverity.Orange,
                    medicine.MedicineId,
                    medicine.Name,
                    $"CẢNH BÁO TỒN KHO: cần {requestedQuantity:0.###} {medicine.Unit}, " +
                    $"hiện còn {medicine.StockQuantity:0.###} {medicine.Unit}.",
                    requestedQuantity,
                    medicine.StockQuantity));
            }
        }

        return warnings;
    }
}
