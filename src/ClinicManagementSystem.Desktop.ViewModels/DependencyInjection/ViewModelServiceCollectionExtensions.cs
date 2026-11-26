using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.Desktop.ViewModels.Admin;
using ClinicManagementSystem.Desktop.ViewModels.Billing;
using ClinicManagementSystem.Desktop.ViewModels.Doctor;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Desktop.ViewModels.Pharmacy;
using ClinicManagementSystem.Desktop.ViewModels.Reception;
using ClinicManagementSystem.Desktop.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.Desktop.ViewModels.DependencyInjection;

public static class ViewModelServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký tầng ViewModel. Vòng đời được chọn có chủ đích:
    ///  - Singleton: thứ sống suốt app (phiên đăng nhập, bộ điều hướng, cửa sổ chính).
    ///  - Transient: mỗi lần mở trang là một ViewModel mới -> dữ liệu luôn tươi, không lẫn trang cũ.
    /// KHÔNG có ViewModel nào giữ DbContext; Service được gọi qua IServiceRunner (một scope mỗi thao tác).
    /// </summary>
    public static IServiceCollection AddClinicViewModels(this IServiceCollection services)
    {
        services.AddSingleton<UserSession>();
        // Tầng Services hỏi "ai đang thao tác?" qua ICurrentUserContext -> trả về đúng phiên đăng nhập.
        services.AddSingleton<ICurrentUserContext>(sp => sp.GetRequiredService<UserSession>());
        services.AddSingleton<IServiceRunner, ScopedServiceRunner>();
        services.AddSingleton<INavigator, Navigator>();
        services.AddSingleton<ShellViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();

        // Phần C - tiếp tân
        services.AddTransient<AppointmentListViewModel>();
        services.AddTransient<BookingViewModel>();
        services.AddTransient<CheckInQueueViewModel>();
        services.AddTransient<CheckInViewModel>();
        services.AddTransient<PatientListViewModel>();
        services.AddTransient<CreatePatientViewModel>();

        // Các vai trò còn lại
        services.AddTransient<DoctorQueueViewModel>();
        services.AddTransient<ConsultationViewModel>();
        services.AddTransient<InvoiceListViewModel>();
        services.AddTransient<PharmacyQueueViewModel>();
        services.AddTransient<UserListViewModel>();
        return services;
    }
}
