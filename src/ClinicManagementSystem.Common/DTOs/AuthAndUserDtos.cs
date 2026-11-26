using ClinicManagementSystem.Common.Enums;

namespace ClinicManagementSystem.Common.DTOs;

// DTO (Data Transfer Object) = "gói dữ liệu" đi qua ranh giới giữa các tầng.
// Tầng Web KHÔNG bao giờ nhận Entity của EF Core; nó chỉ nhận/gửi DTO.
// `record` là kiểu dữ liệu bất biến, tự sinh constructor + so sánh theo giá trị -> rất hợp làm DTO.

/// <summary>Thông tin người dùng sau khi đăng nhập thành công (được ghi vào cookie).</summary>
public sealed record AuthenticatedUserDto(int UserId, string UserName, string DisplayName, UserRole Role);

public sealed record UserListItemDto(
    int UserId,
    string UserName,
    string DisplayName,
    UserRole Role,
    string? Specialty,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Dữ liệu tạo/sửa tài khoản. UserId = null nghĩa là tạo mới.</summary>
public sealed record SaveUserDto(
    int? UserId,
    string UserName,
    string DisplayName,
    UserRole Role,
    string? Specialty,
    bool IsActive,
    string? Password);

public sealed record DoctorOptionDto(int UserId, string DisplayName, string? Specialty);
