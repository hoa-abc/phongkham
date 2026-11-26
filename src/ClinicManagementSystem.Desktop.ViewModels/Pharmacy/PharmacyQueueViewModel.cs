using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Pharmacy;

public sealed class PharmacyRow(PharmacyQueueItemDto dto)
{
    public int PrescriptionId => dto.PrescriptionId;
    public string PatientLine => $"{dto.PatientName} · {dto.MedicalRecordNumber}";
    public string DoctorName => dto.DoctorName;
    public string IssuedAt => dto.IssuedAtUtc is { } t ? Display.DateTimeText(t) : "—";
    public string StatusLabel => dto.Status.ToLabel();
    public string ItemCount => $"{dto.ItemCount} thuốc";
    public bool HasStockIssue => dto.HasStockIssue;
}

public sealed record PharmacyItemRow(string Medicine, string Quantity, string Stock, string Dosage, bool IsEnough);

/// <summary>
/// Dược sĩ: xem đơn đủ điều kiện, CẤP PHÁT hoặc TRẢ VỀ bác sĩ kèm lý do.
/// Không có lệnh nào sửa nội dung đơn - đúng quy tắc "dược sĩ không tự sửa đơn".
/// </summary>
public sealed partial class PharmacyQueueViewModel(IServiceRunner runner, IDialogService dialogs) : PageViewModel
{
    public override string Title => "Đơn chờ cấp";
    public override string? Subtitle => "Chỉ cấp đơn đã thanh toán. Thấy bất thường thì trả về bác sĩ kèm lý do.";

    /// <summary>Bật để xem lịch sử đơn đã cấp/đã trả.</summary>
    [ObservableProperty]
    private bool showHistory;

    public ObservableCollection<PharmacyRow> Items { get; } = [];

    [ObservableProperty]
    private PharmacyRow? selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail), nameof(CanDispense), nameof(CanReturn), nameof(BlockReason), nameof(DetailHeader))]
    private PharmacyPrescriptionDto? detail;

    [ObservableProperty]
    private PatientBanner? patient;

    public ObservableCollection<PharmacyItemRow> DetailItems { get; } = [];
    public ObservableCollection<string> History { get; } = [];

    public bool HasDetail => Detail is not null;
    public bool CanDispense => Detail?.CanDispense == true;
    public bool CanReturn => Detail?.CanReturn == true;
    public string? BlockReason => Detail?.BlockReason;
    public string DetailHeader => Detail is null ? string.Empty
        : $"Đơn #{Detail.PrescriptionId} · {Detail.Status.ToLabel()} · BS {Detail.DoctorName} · Chẩn đoán: {Detail.Diagnosis ?? "—"}";

    protected override async Task OnLoadAsync()
    {
        var keepId = SelectedItem?.PrescriptionId;
        var items = ShowHistory
            ? await runner.RunAsync((IPharmacyService s) => s.GetHistoryAsync())
            : await runner.RunAsync((IPharmacyService s) => s.GetQueueAsync());
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new PharmacyRow(item));
        }

        SelectedItem = Items.FirstOrDefault(x => x.PrescriptionId == keepId) ?? Items.FirstOrDefault();
        if (SelectedItem is null)
        {
            Detail = null;
        }
    }

    partial void OnShowHistoryChanged(bool value) => LoadCommand.Execute(null);

    partial void OnSelectedItemChanged(PharmacyRow? value) => LoadDetailCommand.Execute(null);

    [RelayCommand]
    private Task LoadDetailAsync() => RunBusyAsync(ReloadDetailAsync);

    private async Task ReloadDetailAsync()
    {
        if (SelectedItem is null)
        {
            Detail = null;
            return;
        }

        var detail = await runner.RunAsync((IPharmacyService s) => s.GetAsync(SelectedItem.PrescriptionId));
        Patient = detail is null ? null : PatientBanner.From(detail.Patient);
        DetailItems.Clear();
        History.Clear();
        if (detail is not null)
        {
            foreach (var item in detail.Items)
            {
                DetailItems.Add(new PharmacyItemRow(
                    $"{item.MedicineName} {item.Strength}", $"{Display.Qty(item.Quantity)} {item.Unit}",
                    $"{Display.Qty(item.StockQuantity)} {item.Unit}", item.DosageInstructions ?? string.Empty, item.IsEnough));
            }

            foreach (var h in detail.History)
            {
                History.Add($"{Display.DateTimeText(h.ChangedAtUtc)} · {h.ChangedByName}: {h.FromStatus.ToLabel()} → {h.ToStatus.ToLabel()}"
                            + (h.Reason is null ? string.Empty : $" ({h.Reason})"));
            }
        }

        Detail = detail;
    }

    [RelayCommand]
    private Task DispenseAsync() => RunBusyAsync(async () =>
    {
        if (Detail is null || !dialogs.Confirm("Cấp phát thuốc",
                $"Xác nhận đã giao đủ thuốc của đơn #{Detail.PrescriptionId} cho {Detail.Patient.FullName}? Kho sẽ bị trừ."))
        {
            return;
        }

        Report(await runner.RunAsync((IPharmacyService s) => s.DispenseAsync(Detail.PrescriptionId)));
        await OnLoadAsync();
    }, clearSuccess: true);

    [RelayCommand]
    private Task ReturnToDoctorAsync() => RunBusyAsync(async () =>
    {
        if (Detail is null)
        {
            return;
        }

        var reason = dialogs.AskText("Trả đơn về bác sĩ", "Lý do trả đơn (bác sĩ sẽ thấy lý do này):");
        if (reason is null)
        {
            return;
        }

        Report(await runner.RunAsync((IPharmacyService s) => s.ReturnToDoctorAsync(Detail.PrescriptionId, reason)));
        await OnLoadAsync();
    }, clearSuccess: true);
}
