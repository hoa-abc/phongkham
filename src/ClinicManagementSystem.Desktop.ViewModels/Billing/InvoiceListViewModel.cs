using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Billing;

public sealed class InvoiceRow(InvoiceListItemDto dto)
{
    public int InvoiceId => dto.InvoiceId;
    public string InvoiceNumber => dto.InvoiceNumber;
    public string PatientLine => $"{dto.PatientName} · {dto.MedicalRecordNumber}";
    public string Total => Display.Money(dto.TotalAmount);
    public string StatusLabel => dto.Status.ToLabel() + (dto.NeedsAdjustment ? " · cần điều chỉnh" : string.Empty);
    public string CreatedAt => Display.DateTimeText(dto.CreatedAtUtc);
}

public sealed record InvoiceLineRow(string Description, string Quantity, string UnitPrice, string LineTotal);

/// <summary>Thu ngân: danh sách hóa đơn (trái) + chi tiết hóa đơn đang chọn (phải), thu tiền, hủy/hoàn.</summary>
public sealed partial class InvoiceListViewModel(IServiceRunner runner, IDialogService dialogs) : PageViewModel
{
    public override string Title => "Thanh toán";
    public override string? Subtitle => "Thu tiền hóa đơn sau khám. Hóa đơn đã thu bị khóa, chỉ hủy/hoàn bằng nghiệp vụ điều chỉnh.";

    public IReadOnlyList<Option<InvoiceStatus?>> StatusOptions { get; } =
        [new(null, "Tất cả"), .. Enum.GetValues<InvoiceStatus>().Select(x => new Option<InvoiceStatus?>(x, x.ToLabel()))];

    public IReadOnlyList<Option<PaymentMethod>> PaymentMethods { get; } =
        Enum.GetValues<PaymentMethod>().Select(x => new Option<PaymentMethod>(x, x.ToLabel())).ToList();

    [ObservableProperty]
    private Option<InvoiceStatus?>? selectedStatus;

    [ObservableProperty]
    private Option<PaymentMethod>? selectedMethod;

    public ObservableCollection<InvoiceRow> Items { get; } = [];

    [ObservableProperty]
    private InvoiceRow? selectedInvoice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail), nameof(Total), nameof(CanPay), nameof(CanVoid), nameof(CannotPayReason), nameof(DetailHeader))]
    private InvoiceDetailDto? detail;

    [ObservableProperty]
    private PatientBanner? patient;

    public ObservableCollection<InvoiceLineRow> Lines { get; } = [];

    public bool HasDetail => Detail is not null;
    public string Total => Detail is null ? string.Empty : Display.Money(Detail.TotalAmount);
    public bool CanPay => Detail?.CanPay == true;
    public bool CanVoid => Detail?.CanVoid == true;
    public string? CannotPayReason => Detail?.CanPay == false ? Detail.CannotPayReason : null;
    public string DetailHeader => Detail is null ? string.Empty
        : $"{Detail.InvoiceNumber} · {Detail.Status.ToLabel()} · BS {Detail.DoctorName}";

    protected override async Task OnLoadAsync()
    {
        SelectedStatus ??= StatusOptions.First(x => x.Value == InvoiceStatus.Unpaid);
        SelectedMethod ??= PaymentMethods[0];
        var keepId = SelectedInvoice?.InvoiceId;
        var items = await runner.RunAsync((IBillingService s) => s.ListAsync(SelectedStatus.Value));
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new InvoiceRow(item));
        }

        SelectedInvoice = Items.FirstOrDefault(x => x.InvoiceId == keepId) ?? Items.FirstOrDefault();
        if (SelectedInvoice is null)
        {
            Detail = null;
        }
    }

    partial void OnSelectedStatusChanged(Option<InvoiceStatus?>? oldValue, Option<InvoiceStatus?>? newValue)
    {
        if (oldValue is not null)
        {
            LoadCommand.Execute(null);
        }
    }

    partial void OnSelectedInvoiceChanged(InvoiceRow? value) => LoadDetailCommand.Execute(null);

    [RelayCommand]
    private Task LoadDetailAsync() => RunBusyAsync(ReloadDetailAsync);

    private async Task ReloadDetailAsync()
    {
        if (SelectedInvoice is null)
        {
            Detail = null;
            return;
        }

        var detail = await runner.RunAsync((IBillingService s) => s.GetAsync(SelectedInvoice.InvoiceId));
        Patient = detail is null ? null : PatientBanner.From(detail.Patient);
        Lines.Clear();
        foreach (var line in detail?.Lines ?? [])
        {
            Lines.Add(new InvoiceLineRow(line.Description, $"{Display.Qty(line.Quantity)} {line.UnitLabel}",
                Display.Money(line.UnitPrice), Display.Money(line.LineTotal)));
        }

        Detail = detail;
    }

    [RelayCommand]
    private Task PayAsync() => RunBusyAsync(async () =>
    {
        if (Detail is null || SelectedMethod is null
            || !dialogs.Confirm("Thu tiền", $"Xác nhận thu {Total} ({SelectedMethod.Label}) cho {Detail.InvoiceNumber}?"))
        {
            return;
        }

        var result = await runner.RunAsync((IBillingService s) => s.ProcessPaymentAsync(Detail.InvoiceId, SelectedMethod.Value));
        Report(result);
        await ReloadDetailAsync();
    }, clearSuccess: true);

    [RelayCommand]
    private Task VoidAsync() => RunBusyAsync(async () =>
    {
        if (Detail is null)
        {
            return;
        }

        var reason = dialogs.AskText("Hủy / hoàn hóa đơn", $"Lý do hủy {Detail.InvoiceNumber} (bắt buộc):");
        if (reason is null)
        {
            return;
        }

        Report(await runner.RunAsync((IBillingService s) => s.VoidAsync(Detail.InvoiceId, reason)));
        await ReloadDetailAsync();
    }, clearSuccess: true);
}
