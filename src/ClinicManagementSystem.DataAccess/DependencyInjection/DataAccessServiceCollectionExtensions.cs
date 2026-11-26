using ClinicManagementSystem.DataAccess.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.DataAccess.DependencyInjection;

public static class DataAccessServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký tầng DataAccess vào "thùng chứa" DI. Program.cs chỉ cần gọi đúng một dòng này.
    /// AddDbContext mặc định có vòng đời Scoped = mỗi HTTP request một DbContext riêng.
    /// </summary>
    public static IServiceCollection AddClinicDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ClinicDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<DbInitializer>();
        return services;
    }
}
