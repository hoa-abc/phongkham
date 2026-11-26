using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Desktop.ViewModels.Admin;
using ClinicManagementSystem.Desktop.ViewModels.Billing;
using ClinicManagementSystem.Desktop.ViewModels.Doctor;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Desktop.ViewModels.Pharmacy;
using ClinicManagementSystem.Desktop.ViewModels.Reception;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Shell;

/// <summary>Một mục trên menu trái. Navigate = "bấm vào thì mở trang nào".</summary>
public sealed record MenuItemViewModel(string Title, string Glyph, Action<INavigator> Navigate);

/// <summary>
/// ViewModel của CỬA SỔ CHÍNH (phần việc của A - khung ứng dụng).
/// Giữ: người đang đăng nhập, menu theo vai trò, và trang đang hiển thị (qua Navigator).
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly UserSession session;

    public ShellViewModel(INavigator navigator, UserSession session)
    {
        Navigator = navigator;
        this.session = session;
        session.Changed += (_, _) => OnSessionChanged();
    }

    /// <summary>View bind vào Navigator.CurrentPage để biết hiển thị trang nào.</summary>
    public INavigator Navigator { get; }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = [];

    [ObservableProperty]
    private MenuItemViewModel? selectedMenuItem;

    public bool IsSignedIn => session.IsSignedIn;
    public string UserDisplayName => session.DisplayName ?? string.Empty;
    public string RoleLabel => session.Role?.ToLabel() ?? string.Empty;

    /// <summary>Gọi một lần khi app khởi động: mở màn hình đăng nhập.</summary>
    public void Start() => Navigator.NavigateTo<LoginViewModel>();

    [RelayCommand]
    private void SignOut() => session.SignOut();

    /// <summary>Hàm "partial" do bộ sinh mã gọi mỗi khi SelectedMenuItem đổi -> mở trang tương ứng.</summary>
    partial void OnSelectedMenuItemChanged(MenuItemViewModel? value) => value?.Navigate(Navigator);

    private void OnSessionChanged()
    {
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(RoleLabel));

        MenuItems.Clear();
        SelectedMenuItem = null;
        if (session.Role is not { } role)
        {
            Navigator.NavigateTo<LoginViewModel>();
            return;
        }

        foreach (var item in MenuFor(role))
        {
            MenuItems.Add(item);
        }

        SelectedMenuItem = MenuItems.FirstOrDefault(); // trang đầu tiên của vai trò
    }

    /// <summary>
    /// Menu theo vai trò (giống menu bên web). Ẩn menu chỉ để GỌN giao diện, KHÔNG phải bảo mật:
    /// quyền thật vẫn do PermissionGuard trong tầng Services kiểm tra.
    /// </summary>
    public static IReadOnlyList<MenuItemViewModel> MenuFor(UserRole role) => role switch
    {
        UserRole.Admin =>
        [
            new("Tổng quan", "⌂", n => n.NavigateTo<DashboardViewModel>()),
            new("Lịch hẹn", "▦", n => n.NavigateTo<AppointmentListViewModel>()),
            new("Hồ sơ bệnh nhi", "♡", n => n.NavigateTo<PatientListViewModel>()),
            new("Thanh toán", "₫", n => n.NavigateTo<InvoiceListViewModel>()),
            new("Người dùng", "♙", n => n.NavigateTo<UserListViewModel>())
        ],
        UserRole.Receptionist =>
        [
            new("Tổng quan", "⌂", n => n.NavigateTo<DashboardViewModel>()),
            new("Lịch hẹn", "▦", n => n.NavigateTo<AppointmentListViewModel>()),
            new("Đặt lịch khám", "＋", n => n.NavigateTo<BookingViewModel>()),
            new("Tiếp nhận & Check-in", "✓", n => n.NavigateTo<CheckInQueueViewModel>()),
            new("Hồ sơ bệnh nhi", "♡", n => n.NavigateTo<PatientListViewModel>()),
            new("Thanh toán", "₫", n => n.NavigateTo<InvoiceListViewModel>())
        ],
        UserRole.Doctor =>
        [
            new("Tổng quan", "⌂", n => n.NavigateTo<DashboardViewModel>()),
            new("Hàng đợi khám", "☷", n => n.NavigateTo<DoctorQueueViewModel>()),
            new("Hồ sơ bệnh nhi", "♡", n => n.NavigateTo<PatientListViewModel>())
        ],
        UserRole.Pharmacist =>
        [
            new("Tổng quan", "⌂", n => n.NavigateTo<DashboardViewModel>()),
            new("Đơn chờ cấp", "✚", n => n.NavigateTo<PharmacyQueueViewModel>())
        ],
        _ => []
    };
}

/// <summary>Màn hình đăng nhập.</summary>
public sealed partial class LoginViewModel(IServiceRunner runner, UserSession session) : PageViewModel
{
    public override string Title => "Đăng nhập";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string userName = string.Empty;

    /// <summary>
    /// KHÔNG có [ObservableProperty]: PasswordBox của WPF cố ý không cho binding (để mật khẩu không nằm
    /// lung tung trong bộ nhớ). View gán giá trị này trong code-behind khi người dùng gõ.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private Task SignInAsync() => RunBusyAsync(async () =>
    {
        var result = await runner.RunAsync((IAuthService s) => s.SignInAsync(UserName.Trim(), Password));
        if (!result.Succeeded)
        {
            ErrorMessage = result.Message;
            return;
        }

        Password = string.Empty;
        session.SignIn(result.Value!); // ShellViewModel nghe sự kiện này để dựng menu và mở trang đầu
    }, clearSuccess: true);

    private bool CanSignIn() => !string.IsNullOrWhiteSpace(UserName);
}

/// <summary>Một ô số liệu trên trang Tổng quan.</summary>
public sealed record StatTile(string Label, string Value, string Hint);

/// <summary>Trang Tổng quan: mỗi vai trò thấy con số liên quan tới công việc của mình.</summary>
public sealed class DashboardViewModel(IServiceRunner runner, UserSession session) : PageViewModel
{
    public override string Title => "Tổng quan";
    public override string? Subtitle => $"Xin chào {session.DisplayName} · {Display.Date(Common.Time.ClinicClock.Today)}";

    public ObservableCollection<StatTile> Tiles { get; } = [];
    public ObservableCollection<AppointmentRow> TodaySchedule { get; } = [];
    public bool HasSchedule => TodaySchedule.Count > 0;

    protected override async Task OnLoadAsync()
    {
        var d = await runner.RunAsync((IDashboardService s) => s.GetAsync());
        Tiles.Clear();
        foreach (var tile in TilesFor(d))
        {
            Tiles.Add(tile);
        }

        TodaySchedule.Clear();
        foreach (var item in d.TodaySchedule)
        {
            TodaySchedule.Add(new AppointmentRow(item));
        }

        OnPropertyChanged(nameof(HasSchedule));
    }

    public static IEnumerable<StatTile> TilesFor(DashboardDto d) => d.Role switch
    {
        UserRole.Doctor =>
        [
            new("Đang chờ khám", d.WaitingForDoctor.ToString(), "Bé đã check-in, chờ bạn khám"),
            new("Đơn bị trả về", d.ReturnedPrescriptions.ToString(), "Dược sĩ trả lại, cần sửa")
        ],
        UserRole.Pharmacist =>
        [
            new("Đơn chờ cấp", d.ReadyToDispense.ToString(), "Đã thanh toán, đủ điều kiện cấp"),
            new("Thuốc sắp hết", d.LowStockMedicines.ToString(), "Tồn kho dưới mức đặt lại")
        ],
        UserRole.Admin =>
        [
            new("Lịch hẹn hôm nay", d.TodayAppointments.ToString(), $"{d.CheckedInToday} bé đã check-in"),
            new("Doanh thu hôm nay", Display.Money(d.RevenueToday), "Các hóa đơn đã thu"),
            new("Doanh thu tháng", Display.Money(d.RevenueThisMonth), "Từ đầu tháng tới nay"),
            new("Thuốc sắp hết", d.LowStockMedicines.ToString(), "Cần nhập thêm")
        ],
        _ =>
        [
            new("Lịch hẹn hôm nay", d.TodayAppointments.ToString(), "Tất cả bác sĩ"),
            new("Đã check-in", d.CheckedInToday.ToString(), "Đang ở phòng khám"),
            new("Hóa đơn chờ thu", d.UnpaidInvoices.ToString(), "Khám xong, chưa thanh toán"),
            new("Tổng hồ sơ bệnh nhi", d.TotalPatients.ToString(), "Đang hoạt động")
        ]
    };
}
