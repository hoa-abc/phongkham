using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Reception;

/// <summary>Hàng chờ check-in: các lịch "Đã đặt" của hôm nay.</summary>
public sealed partial class CheckInQueueViewModel(IServiceRunner runner, INavigator navigator) : PageViewModel
{
    public override string Title => "Tiếp nhận & Check-in";
    public override string? Subtitle => $"Lịch đã hẹn chờ tiếp nhận · {Display.Date(ClinicClock.Today)}";

    public ObservableCollection<AppointmentRow> Items { get; } = [];
    public bool IsEmpty => Items.Count == 0;

    protected override async Task OnLoadAsync()
    {
        var items = await runner.RunAsync((IReceptionService s) => s.GetQueueAsync(ClinicClock.Today));
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new AppointmentRow(item));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void OpenCheckIn(AppointmentRow? row)
    {
        if (row is not null)
        {
            navigator.NavigateTo<CheckInViewModel>(page => page.AppointmentId = row.AppointmentId);
        }
    }

    [RelayCommand]
    private void NewBooking() => navigator.NavigateTo<BookingViewModel>();
}

/// <summary>
/// Ghi sinh hiệu và check-in. Minh họa KIỂM TRA DỮ LIỆU trong MVVM:
/// [Required], [Range] giống hệt ViewModel bên web, nhưng ở đây ObservableValidator kiểm tra
/// NGAY KHI GÕ ([NotifyDataErrorInfo]) và WPF tự viền đỏ ô sai - không cần bấm gửi mới biết.
/// </summary>
public sealed partial class CheckInViewModel(IServiceRunner runner, INavigator navigator) : PageViewModel
{
    public override string Title => "Ghi nhận sinh hiệu";
    public override string? Subtitle => "Xác minh đúng bệnh nhi trước khi ghi chỉ số tại quầy.";

    public int AppointmentId { get; set; }

    [ObservableProperty]
    private PatientBanner? patient;

    [ObservableProperty]
    private AppointmentRow? appointment;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng nhập cân nặng.")]
    [Range(1d, 150d, ErrorMessage = "Cân nặng phải từ 1 đến 150 kg.")]
    private decimal? weightKg;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng nhập chiều cao.")]
    [Range(30d, 200d, ErrorMessage = "Chiều cao phải từ 30 đến 200 cm.")]
    private decimal? heightCm;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyPropertyChangedFor(nameof(IsFever))]
    [Required(ErrorMessage = "Vui lòng nhập nhiệt độ.")]
    [Range(30d, 45d, ErrorMessage = "Nhiệt độ phải từ 30 đến 45 °C.")]
    private decimal? temperatureC;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng nhập nhịp tim.")]
    [Range(30, 250, ErrorMessage = "Nhịp tim phải từ 30 đến 250 lần/phút.")]
    private int? heartRate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(1000)]
    private string? symptoms;

    /// <summary>Sốt từ 38 °C: hiện cảnh báo vàng NGAY khi gõ (web phải gửi form mới thấy).</summary>
    public bool IsFever => TemperatureC >= 38;

    protected override async Task OnLoadAsync()
    {
        var context = await runner.RunAsync((IReceptionService s) => s.GetCheckInContextAsync(AppointmentId));
        if (context is null)
        {
            ErrorMessage = "Không tìm thấy lịch hẹn.";
            return;
        }

        Patient = PatientBanner.From(context.Patient);
        Appointment = new AppointmentRow(context.Appointment);
        Symptoms ??= context.Appointment.Reason;
    }

    [RelayCommand]
    private Task ConfirmAsync() => RunBusyAsync(async () =>
    {
        ValidateAllProperties(); // kiểm tra cả những ô người dùng chưa chạm vào
        if (HasErrors)
        {
            ErrorMessage = "Vui lòng kiểm tra lại các chỉ số được viền đỏ.";
            return;
        }

        var result = await runner.RunAsync((IReceptionService s) => s.CheckInAsync(new CheckInDto(
            AppointmentId, WeightKg!.Value, HeightCm!.Value, TemperatureC!.Value, HeartRate!.Value, Symptoms)));
        if (!Report(result))
        {
            return;
        }

        var message = result.Message;
        navigator.NavigateTo<CheckInQueueViewModel>(page => page.SuccessMessage = message);
    }, clearSuccess: true);

    [RelayCommand]
    private void Back() => navigator.NavigateTo<CheckInQueueViewModel>();
}
