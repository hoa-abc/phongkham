using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Security;

namespace ClinicManagementSystem.Services.Authorization;

/// <summary>
/// Kiểm tra quyền NGAY TRONG tầng nghiệp vụ. Ẩn nút trên giao diện hay gắn [Authorize] ở Controller
/// chỉ là lớp ngoài; nếu ai đó gọi thẳng Service (hoặc tự gõ URL) thì lớp này vẫn chặn.
/// Dòng đầu tiên của mọi hàm nghiệp vụ là guard.Require(...).
/// </summary>
public sealed class PermissionGuard(ICurrentUserContext currentUser)
{
    public void Require(string actionName, params UserRole[] allowedRoles)
    {
        if (currentUser.UserId is null || currentUser.Role is null)
        {
            throw new UnauthorizedAccessException($"Bạn cần đăng nhập để thực hiện: {actionName}.");
        }

        if (!allowedRoles.Contains(currentUser.Role.Value))
        {
            var allowed = string.Join(", ", allowedRoles.Select(role => role.ToLabel()));
            throw new UnauthorizedAccessException(
                $"Bạn không có quyền thực hiện: {actionName}. Chức năng này chỉ dành cho {allowed}.");
        }
    }

    /// <summary>Chỉ cần đã đăng nhập, vai trò nào cũng được.</summary>
    public void RequireSignedIn(string actionName)
        => Require(actionName, UserRole.Admin, UserRole.Receptionist, UserRole.Doctor, UserRole.Pharmacist);
}
