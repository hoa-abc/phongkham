using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.Services.Implementations;

public sealed class AuthService(ClinicDbContext db) : IAuthService
{
    // Hash "mồi" để khi tên đăng nhập không tồn tại ta vẫn tốn thời gian băm như bình thường:
    // kẻ dò tài khoản không phân biệt được "sai tên" với "sai mật khẩu" qua tốc độ phản hồi.
    private static readonly string DummyHash = PasswordHasher.Hash("dummy-password-for-timing");

    public async Task<ServiceResult<AuthenticatedUserDto>> SignInAsync(
        string userName, string password, CancellationToken ct = default)
    {
        var normalized = (userName ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || string.IsNullOrEmpty(password))
        {
            return ServiceResult<AuthenticatedUserDto>.Failure(
                "CREDENTIALS_REQUIRED", "Vui lòng nhập tên đăng nhập và mật khẩu.");
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.UserName == normalized, ct);
        var passwordOk = PasswordHasher.Verify(password, user?.PasswordHash ?? DummyHash);

        if (user is null || !passwordOk || !user.IsActive)
        {
            // Ghi nhật ký đăng nhập thất bại (không bao giờ ghi mật khẩu đã nhập).
            AuditLogWriter.Add(db, user?.UserId, normalized.Length > 100 ? normalized[..100] : normalized, null,
                "Auth.SignInFailed", nameof(User), user?.UserId ?? 0, null, null);
            await db.SaveChangesAsync(ct);

            // Thông báo chung chung có chủ đích: không tiết lộ tài khoản có tồn tại hay không.
            return ServiceResult<AuthenticatedUserDto>.Failure(
                "INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");
        }

        AuditLogWriter.Add(db, user.UserId, user.DisplayName, null, "Auth.SignedIn", nameof(User), user.UserId, null, null);
        await db.SaveChangesAsync(ct);

        return ServiceResult<AuthenticatedUserDto>.Success(
            new AuthenticatedUserDto(user.UserId, user.UserName, user.DisplayName, user.Role));
    }

    public Task<bool> IsUserActiveAsync(int userId, CancellationToken ct = default)
        => db.Users.AsNoTracking().AnyAsync(x => x.UserId == userId && x.IsActive, ct);
}
