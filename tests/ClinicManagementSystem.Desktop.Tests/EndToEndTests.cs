using System.Net.Sockets;
using System.Text.Json;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.DataAccess;
using ClinicManagementSystem.DataAccess.DependencyInjection;
using ClinicManagementSystem.DataAccess.Seeding;
using ClinicManagementSystem.Desktop.ViewModels.Billing;
using ClinicManagementSystem.Desktop.ViewModels.DependencyInjection;
using ClinicManagementSystem.Desktop.ViewModels.Doctor;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Desktop.ViewModels.Pharmacy;
using ClinicManagementSystem.Desktop.ViewModels.Reception;
using ClinicManagementSystem.Desktop.ViewModels.Shell;
using ClinicManagementSystem.Services.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.Desktop.Tests;

/// <summary>
/// KIỂM THỬ TOÀN LUỒNG qua ViewModel với SQL Server THẬT (database riêng ClinicDesktopTests).
/// Đi đúng thứ tự người dùng bấm: tiếp tân đặt lịch + check-in -> bác sĩ khám, kê đơn -> thu ngân thu tiền
/// -> dược sĩ cấp thuốc. Chỉ thiếu phần "vẽ cửa sổ" (cần Windows); mọi logic phía sau đều chạy thật.
/// Tự bỏ qua nếu chưa bật SQL Server.
/// </summary>
public sealed class EndToEndTests : IAsyncLifetime
{
    private const string Password = "Desktop@Test-123";
    private static readonly string? ConnectionString = ResolveConnectionString();
    private ServiceProvider? provider;

    public async Task InitializeAsync()
    {
        if (ConnectionString is null)
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddClinicDataAccess(ConnectionString);
        services.AddClinicBusinessServices();
        services.AddClinicViewModels();
        services.AddSingleton<IDialogService>(new FakeDialogs(confirm: true));
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        await db.Database.EnsureDeletedAsync();
        await scope.ServiceProvider.GetRequiredService<DbInitializer>().InitializeAsync(Password);
    }

    public Task DisposeAsync()
    {
        provider?.Dispose();
        return Task.CompletedTask;
    }

    private T Get<T>() where T : notnull => provider!.GetRequiredService<T>();

    private async Task SignInAsync(string userName)
    {
        Get<UserSession>().SignOut();
        var login = Get<LoginViewModel>();
        login.UserName = userName;
        login.Password = Password;
        await login.SignInCommand.ExecuteAsync(null);
        Assert.True(Get<UserSession>().IsSignedIn, login.ErrorMessage);
    }

    /// <summary>Mở trang như Navigator làm, rồi CHỜ mọi việc tải nền (lưới giờ...) chạy xong.</summary>
    private static async Task<T> OpenAsync<T>(T page) where T : PageViewModel
    {
        await page.LoadCommand.ExecuteAsync(null);
        if (page is BookingViewModel booking && booking.LoadSlotsCommand.ExecutionTask is { } slots)
        {
            await slots;
        }

        Assert.False(page.HasError, page.ErrorMessage);
        return page;
    }

    [SqlServerDesktopFact]
    public async Task Toan_luong_tu_dat_lich_den_cap_thuoc_qua_ViewModel()
    {
        // ===== Phần A: khởi động như App.xaml.cs (cửa sổ chính có trước), rồi đăng nhập =====
        var shell = Get<ShellViewModel>();
        shell.Start();
        Assert.IsType<LoginViewModel>(Get<INavigator>().CurrentPage);
        await SignInAsync("letan");
        Assert.Contains(shell.MenuItems, x => x.Title == "Đặt lịch khám");
        Assert.IsType<DashboardViewModel>(Get<INavigator>().CurrentPage);

        // ===== Phần C: hai quầy cùng mở trang đặt lịch, cùng chọn MỘT khung giờ ngày mai =====
        var counterA = await OpenAsync(Get<BookingViewModel>());
        var counterB = await OpenAsync(Get<BookingViewModel>());
        foreach (var vm in new[] { counterA, counterB })
        {
            vm.SelectedDate = TestDates.Tomorrow;
            await vm.LoadSlotsCommand.ExecuteAsync(null);
            vm.SelectedPatient = vm.Patients[0];
        }

        var slotLabel = counterA.Slots.First(x => x.IsAvailable).Label;
        counterA.SelectedSlot = counterA.Slots.First(x => x.Label == slotLabel);
        counterB.SelectedSlot = counterB.Slots.First(x => x.Label == slotLabel);
        Assert.True(counterB.ConfirmCommand.CanExecute(null)); // lúc này quầy B vẫn thấy khung trống

        await counterA.ConfirmCommand.ExecuteAsync(null);
        await counterB.ConfirmCommand.ExecuteAsync(null);

        Assert.Contains("Đặt lịch thành công", counterA.SuccessMessage);
        Assert.Equal("Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.", counterB.ErrorMessage);
        Assert.False(counterB.Slots.First(x => x.Label == slotLabel).IsAvailable); // lưới của B đã cập nhật
        Assert.Equal("Đã đặt", counterB.Slots.First(x => x.Label == slotLabel).Note);

        // Lịch vừa đặt hiện trong danh sách ngày mai, kèm mã bé
        var list = Get<AppointmentListViewModel>();
        list.SelectedDate = TestDates.Tomorrow;
        await OpenAsync(list);
        Assert.Contains(list.Items, x => x.Time == slotLabel && x.MedicalRecordNumber == counterA.Patients[0].MedicalRecordNumber);

        // ===== Phần C: check-in một bé của bác sĩ "bacsi" hôm nay =====
        var queue = await OpenAsync(Get<CheckInQueueViewModel>());
        var row = queue.Items.First(x => x.DoctorName.Contains("Võ Tuấn Kiệt"));
        var checkIn = Get<CheckInViewModel>();
        checkIn.AppointmentId = row.AppointmentId;
        await OpenAsync(checkIn);
        checkIn.WeightKg = 14.2m;
        checkIn.HeightCm = 96;
        checkIn.HeartRate = 105;
        checkIn.TemperatureC = 37.8m;
        await checkIn.ConfirmCommand.ExecuteAsync(null);
        Assert.False(checkIn.HasError, checkIn.ErrorMessage);
        var afterCheckIn = Assert.IsType<CheckInQueueViewModel>(Get<INavigator>().CurrentPage);
        Assert.Contains("Check-in thành công", afterCheckIn.SuccessMessage);

        // ===== Bác sĩ khám và kê đơn =====
        await SignInAsync("bacsi");
        var doctorQueue = await OpenAsync(Get<DoctorQueueViewModel>());
        var visit = doctorQueue.Items.First(x => x.PatientName == row.PatientName);
        var consult = Get<ConsultationViewModel>();
        consult.VisitId = visit.VisitId;
        await OpenAsync(consult);
        Assert.True(consult.IsWaiting);
        await consult.StartCommand.ExecuteAsync(null);
        Assert.True(consult.CanEdit, consult.ErrorMessage);

        consult.Diagnosis = "Viêm họng cấp (dữ liệu kiểm thử)";
        consult.AddLineCommand.Execute(null);
        consult.Lines[0].Medicine = consult.MedicineOptions.First(x => x.StockQuantity >= 1);
        consult.Lines[0].Quantity = 1;
        consult.Lines[0].DosageInstructions = "Uống sau ăn";
        consult.AcknowledgeWarnings = true;
        if (!consult.Services.Any(x => x.IsSelected))
        {
            consult.Services[0].IsSelected = true;
        }

        await consult.CompleteCommand.ExecuteAsync(null);
        Assert.False(consult.HasError, consult.ErrorMessage);
        Assert.IsType<DoctorQueueViewModel>(Get<INavigator>().CurrentPage);

        // ===== Thu ngân thu tiền =====
        await SignInAsync("letan");
        var billing = await OpenAsync(Get<InvoiceListViewModel>());
        billing.SelectedInvoice = billing.Items.First(x => x.PatientLine.Contains(row.MedicalRecordNumber));
        await billing.LoadDetailCommand.ExecuteAsync(null);
        Assert.True(billing.CanPay, billing.CannotPayReason);
        await billing.PayCommand.ExecuteAsync(null);
        Assert.False(billing.HasError, billing.ErrorMessage);
        Assert.False(billing.CanPay); // hóa đơn đã khóa

        // ===== Dược sĩ cấp thuốc =====
        await SignInAsync("duocsi");
        var pharmacy = await OpenAsync(Get<PharmacyQueueViewModel>());
        pharmacy.SelectedItem = pharmacy.Items.First(x => x.PatientLine.Contains(row.MedicalRecordNumber));
        await pharmacy.LoadDetailCommand.ExecuteAsync(null);
        Assert.True(pharmacy.CanDispense, pharmacy.BlockReason);
        await pharmacy.DispenseCommand.ExecuteAsync(null);
        Assert.False(pharmacy.HasError, pharmacy.ErrorMessage);
        Assert.DoesNotContain(pharmacy.Items, x => x.PatientLine.Contains(row.MedicalRecordNumber));
    }

    [SqlServerDesktopFact]
    public async Task Bac_si_goi_thang_ViewModel_dat_lich_van_bi_tang_Services_chan()
    {
        // Ẩn menu chỉ để gọn giao diện. Kể cả khi ViewModel đặt lịch bị mở bằng cách nào đó,
        // PermissionGuard trong tầng Services vẫn chặn và ViewModel hiện lỗi thay vì làm sập app.
        await SignInAsync("bacsi");
        var booking = Get<BookingViewModel>();
        await booking.LoadCommand.ExecuteAsync(null);

        Assert.True(booking.HasError);
        Assert.Contains("quyền", booking.ErrorMessage);
    }

    private static string? ResolveConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("CLINIC_DB_CONNECTION");
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); raw is null && dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "ClinicManagementSystem.Web", "appsettings.Development.json");
            if (File.Exists(path))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                raw = json.RootElement.GetProperty("ConnectionStrings").GetProperty("ClinicDb").GetString();
            }
        }

        if (raw is null)
        {
            return null;
        }

        // Database RIÊNG cho test desktop: không đụng dữ liệu của app và của bộ test bản web.
        var builder = new SqlConnectionStringBuilder(raw) { InitialCatalog = "ClinicDesktopTests", ConnectTimeout = 5 };
        try
        {
            var parts = builder.DataSource.Split(',');
            using var client = new TcpClient();
            return client.ConnectAsync(parts[0], parts.Length > 1 ? int.Parse(parts[1]) : 1433).Wait(TimeSpan.FromSeconds(2))
                ? builder.ConnectionString
                : null;
        }
        catch
        {
            return null;
        }
    }

    public sealed class SqlServerDesktopFactAttribute : FactAttribute
    {
        public SqlServerDesktopFactAttribute()
        {
            if (ConnectionString is null)
            {
                Skip = "Chưa bật SQL Server (docker compose up -d).";
            }
        }
    }
}
