using ClinicManagementSystem.Services.Authorization;
using ClinicManagementSystem.Services.Contracts;
using ClinicManagementSystem.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khai báo với DI container: "khi ai xin IAppointmentService thì đưa cho họ một AppointmentService".
    /// Scoped = mỗi HTTP request tạo một bộ service riêng, dùng chung một DbContext của request đó,
    /// hết request thì hủy. Nhờ vậy dữ liệu đang sửa dở của request này không lẫn sang request khác.
    /// </summary>
    public static IServiceCollection AddClinicBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<PermissionGuard>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IReceptionService, ReceptionService>();
        services.AddScoped<IConsultationService, ConsultationService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IPharmacyService, PharmacyService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
