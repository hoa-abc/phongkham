using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Reception;

/// <summary>Danh sách lịch hẹn: lọc theo ngày/trạng thái, tìm theo mã/tên/SĐT, check-in, hủy lịch.</summary>
public sealed partial class AppointmentListViewModel(
    IServiceRunner runner, INavigator navigator, IDialogService dialogs) : PageViewModel
{
    public override string Title => "Lịch hẹn";
    public override string? Subtitle => "Tra cứu lịch, kiểm tra trạng thái và chuyển bệnh nhi sang tiếp nhận.";

    [ObservableProperty]
    private string? keyword;

    [ObservableProperty]
    private DateTime? selectedDate = ClinicClock.Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>Tích "Mọi ngày" thì bỏ lọc ngày.</summary>
    [ObservableProperty]
    private bool allDates;

    /// <summary>Danh sách lựa chọn trạng thái; giá trị null = "Tất cả".</summary>
    public IReadOnlyList<Option<AppointmentStatus?>> StatusOptions { get; } =
        [new(null, "Tất cả"), .. Enum.GetValues<AppointmentStatus>().Select(x => new Option<AppointmentStatus?>(x, x.ToLabel()))];

    [ObservableProperty]
    private Option<AppointmentStatus?>? selectedStatus;

    public ObservableCollection<AppointmentRow> Items { get; } = [];

    public string ResultSummary => Items.Count == 0 ? "Không có lịch phù hợp bộ lọc." : $"{Items.Count} lịch hẹn";

    protected override async Task OnLoadAsync()
    {
        DateOnly? date = AllDates || SelectedDate is null ? null : DateOnly.FromDateTime(SelectedDate.Value);
        var items = await runner.RunAsync((IAppointmentService s) =>
            s.SearchAsync(new AppointmentFilterDto(date, SelectedStatus?.Value, Keyword)));
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new AppointmentRow(item));
        }

        OnPropertyChanged(nameof(ResultSummary));
    }

    [RelayCommand]
    private void NewBooking() => navigator.NavigateTo<BookingViewModel>();

    [RelayCommand]
    private void CheckIn(AppointmentRow? row)
    {
        if (row is { CanAct: true })
        {
            navigator.NavigateTo<CheckInViewModel>(page => page.AppointmentId = row.AppointmentId);
        }
    }

    [RelayCommand]
    private Task CancelAsync(AppointmentRow? row) => RunBusyAsync(async () =>
    {
        if (row is not { CanAct: true })
        {
            return;
        }

        // Hủy là thao tác khó hoàn tác -> hỏi lý do. Bấm "Hủy" trên hộp thoại = không làm gì.
        var reason = dialogs.AskText("Hủy lịch hẹn",
            $"Lý do hủy lịch {row.Time} {row.Date} của {row.PatientName} ({row.MedicalRecordNumber}):");
        if (reason is null)
        {
            return;
        }

        var result = await runner.RunAsync((IAppointmentService s) => s.CancelAsync(row.AppointmentId, reason));
        Report(result);
        await OnLoadAsync();
    }, clearSuccess: true);
}
