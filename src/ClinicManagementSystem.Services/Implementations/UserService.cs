using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class UserService(
    ClinicDbContext db,
    PermissionGuard guard,
    ICurrentUserContext currentUser) : IUserService
{
    private const int MinPasswordLength = 8;

    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken ct = default)
    {
        guard.Require("xem danh sách người dùng", UserRole.Admin);
        return await db.Users.AsNoTracking()
            .OrderBy(x => x.Role).ThenBy(x => x.UserName)
            .Select(x => new UserListItemDto(x.UserId, x.UserName, x.DisplayName, x.Role, x.Specialty, x.IsActive, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<UserListItemDto?> GetAsync(int userId, CancellationToken ct = default)
    {
        guard.Require("xem tài khoản", UserRole.Admin);
        return await db.Users.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new UserListItemDto(x.UserId, x.UserName, x.DisplayName, x.Role, x.Specialty, x.IsActive, x.CreatedAtUtc))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<int>> SaveAsync(SaveUserDto dto, CancellationToken ct = default)
    {
        guard.Require("quản lý tài khoản", UserRole.Admin);

        var userName = (dto.UserName ?? string.Empty).Trim().ToLowerInvariant();
        var displayName = (dto.DisplayName ?? string.Empty).Trim();
        if (userName.Length is < 3 or > 100 || userName.Any(char.IsWhiteSpace) || displayName.Length is 0 or > 160
            || !Enum.IsDefined(dto.Role))
        {
            return ServiceResult<int>.Failure("USER_INVALID",
                "Tên đăng nhập (3-100 ký tự, không có khoảng trắng), họ tên và vai trò là bắt buộc.");
        }

        var isNew = dto.UserId is null;
        if ((isNew || !string.IsNullOrEmpty(dto.Password)) && (dto.Password?.Length ?? 0) < MinPasswordLength)
        {
            return ServiceResult<int>.Failure("PASSWORD_TOO_SHORT", $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự.");
        }

        // Transaction: tài khoản và dòng nhật ký của nó phải cùng được lưu, hoặc cùng không.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        User user;
        object? before = null;
        if (isNew)
        {
            user = new User { CreatedAtUtc = DateTime.UtcNow };
            db.Users.Add(user);
        }
        else
        {
            var existing = await db.Users.SingleOrDefaultAsync(x => x.UserId == dto.UserId, ct);
            if (existing is null)
            {
                return ServiceResult<int>.Failure("USER_NOT_FOUND", "Không tìm thấy tài khoản.");
            }

            // Admin không được tự khóa hoặc tự hạ quyền chính mình -> tránh hệ thống không còn ai quản trị.
            if (existing.UserId == currentUser.UserId && (!dto.IsActive || dto.Role != UserRole.Admin))
            {
                return ServiceResult<int>.Failure("CANNOT_LOCK_SELF", "Bạn không thể tự khóa hoặc tự đổi vai trò của chính mình.");
            }

            user = existing;
            before = new { user.UserName, user.DisplayName, user.Role, user.Specialty, user.IsActive };
        }

        user.UserName = userName;
        user.DisplayName = displayName;
        user.Role = dto.Role;
        user.Specialty = dto.Role == UserRole.Doctor && !string.IsNullOrWhiteSpace(dto.Specialty) ? dto.Specialty.Trim() : null;
        user.IsActive = dto.IsActive;
        var passwordChanged = !string.IsNullOrEmpty(dto.Password);
        if (passwordChanged)
        {
            user.PasswordHash = PasswordHasher.Hash(dto.Password!);
        }

        try
        {
            await db.SaveChangesAsync(ct); // cần UserId của tài khoản mới để ghi nhật ký
        }
        catch (DbUpdateException exception) when (SqlServerErrorDetector.IsUniqueConstraintViolation(exception))
        {
            return ServiceResult<int>.Failure("USERNAME_TAKEN", "Tên đăng nhập này đã được sử dụng.");
        }

        // Nhật ký chỉ ghi "có đổi mật khẩu hay không", không bao giờ ghi mật khẩu hay hash.
        AuditLogWriter.Add(db, currentUser, isNew ? "User.Created" : "User.Updated", nameof(User), user.UserId, before,
            new { user.UserName, user.DisplayName, user.Role, user.Specialty, user.IsActive, PasswordChanged = passwordChanged });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<int>.Success(user.UserId, isNew ? "Đã tạo tài khoản." : "Đã cập nhật tài khoản.");
    }

    public async Task<IReadOnlyList<DoctorOptionDto>> GetDoctorsAsync(CancellationToken ct = default)
    {
        guard.RequireSignedIn("xem danh sách bác sĩ");
        return await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Doctor && x.IsActive)
            .OrderBy(x => x.UserId)
            .Select(x => new DoctorOptionDto(x.UserId, x.DisplayName, x.Specialty))
            .ToListAsync(ct);
    }
}
