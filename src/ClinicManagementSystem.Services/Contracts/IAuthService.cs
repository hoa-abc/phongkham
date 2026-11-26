using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;

namespace ClinicManagementSystem.Services.Contracts;

// Interface = "bản hợp đồng": Controller chỉ biết CÓ những hàm này, không biết bên trong làm thế nào.
// Nhờ đó có thể thay bản cài đặt (ví dụ bản giả để test) mà không sửa Controller.

public interface IAuthService
{
    Task<ServiceResult<AuthenticatedUserDto>> SignInAsync(string userName, string password, CancellationToken ct = default);

    /// <summary>Dùng để kiểm tra lại cookie mỗi request: tài khoản bị khóa thì bị đăng xuất ngay.</summary>
    Task<bool> IsUserActiveAsync(int userId, CancellationToken ct = default);
}

public interface IUserService
{
    Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken ct = default);
    Task<UserListItemDto?> GetAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveAsync(SaveUserDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorOptionDto>> GetDoctorsAsync(CancellationToken ct = default);
}
