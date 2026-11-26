using System.Windows;
using System.Windows.Threading;
using ClinicManagementSystem.DataAccess.DependencyInjection;
using ClinicManagementSystem.DataAccess.Seeding;
using ClinicManagementSystem.Desktop.Services;
using ClinicManagementSystem.Desktop.ViewModels.DependencyInjection;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Desktop.ViewModels.Shell;
using ClinicManagementSystem.Services.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.Desktop;

/// <summary>
/// "Composition root" của app desktop - vai trò giống Program.cs bên web (phần việc của A):
///  1. Đọc cấu hình (chuỗi kết nối).
///  2. Đăng ký DI: DataAccess + Services (DÙNG LẠI NGUYÊN của bản web) + ViewModels + dịch vụ WPF.
///  3. Khởi tạo database (migration + dữ liệu mẫu) nếu được bật.
///  4. Mở cửa sổ chính và màn hình đăng nhập.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        // ---------- 1. Cấu hình ----------
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true) // chứa mật khẩu, KHÔNG đưa lên Git
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("CLINIC_DB_CONNECTION")
                               ?? configuration.GetConnectionString("ClinicDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show(
                "Chưa cấu hình chuỗi kết nối database.\nHãy chép appsettings.Local.example.json thành appsettings.Local.json và điền mật khẩu (xem README).",
                "Phòng khám Nhi Đồng", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // ---------- 2. Đăng ký DI ----------
        var collection = new ServiceCollection();
        collection.AddClinicDataAccess(connectionString);     // tầng DataAccess (của B) - y như web
        collection.AddClinicBusinessServices();               // tầng Services - y như web
        collection.AddClinicViewModels();                     // tầng ViewModel (MVVM)
        collection.AddSingleton<IDialogService, WpfDialogService>();
        collection.AddSingleton<MainWindow>();

        // ValidateScopes/ValidateOnBuild: báo lỗi NGAY khi khởi động nếu đăng ký DI sai
        // (ví dụ một Singleton giữ DbContext Scoped - lỗi "captive dependency").
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // ---------- 3. Database ----------
        if (bool.TryParse(configuration["Seed:Enabled"], out var seed) && seed)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DbInitializer>()
                    .InitializeAsync(configuration["Seed:DemoPassword"] ?? string.Empty);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Không khởi tạo được database. Kiểm tra SQL Server đã bật (docker compose up -d) và chuỗi kết nối.\n\n" + exception.Message,
                    "Phòng khám Nhi Đồng", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
        }

        // ---------- 4. Cửa sổ chính ----------
        var window = services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
        services.GetRequiredService<ShellViewModel>().Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Lưới an toàn cuối: lỗi không ai bắt -> báo lỗi thay vì app tự tắt.</summary>
    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Có lỗi không mong muốn:\n" + e.Exception.Message, "Phòng khám Nhi Đồng",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
