using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Doctor;

public sealed class DoctorQueueRow(DoctorQueueItemDto dto)
{
    public int VisitId => dto.VisitId;
    public string Time => Display.Time(dto.StartsAtUtc);
    public string PatientName => dto.PatientName;
    public string MedicalRecordNumber => dto.MedicalRecordNumber;
    public string Age => Display.Age(dto.DateOfBirth);
    public string StatusLabel => dto.VisitStatus.ToLabel();
    public string PrescriptionLabel => dto.PrescriptionStatus?.ToLabel() ?? "Chưa kê đơn";
    public string? ReturnReason => dto.ReturnReason is null ? null : "Dược sĩ trả về: " + dto.ReturnReason;
    public bool HasReturnReason => dto.ReturnReason is not null;
    public bool HasAllergies => dto.HasAllergies;
}

/// <summary>Hàng đợi khám của bác sĩ đang đăng nhập.</summary>
public sealed partial class DoctorQueueViewModel(IServiceRunner runner, INavigator navigator) : PageViewModel
{
    public override string Title => "Hàng đợi khám";
    public override string? Subtitle => "Các bé đã check-in và được phân cho bạn.";

    public ObservableCollection<DoctorQueueRow> Items { get; } = [];
    public bool IsEmpty => Items.Count == 0;

    protected override async Task OnLoadAsync()
    {
        var items = await runner.RunAsync((IConsultationService s) => s.GetQueueAsync());
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new DoctorQueueRow(item));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Open(DoctorQueueRow? row)
    {
        if (row is not null)
        {
            navigator.NavigateTo<ConsultationViewModel>(page => page.VisitId = row.VisitId);
        }
    }
}

/// <summary>Một dòng thuốc trên đơn. Là ObservableObject vì bác sĩ sửa trực tiếp trên bảng.</summary>
public sealed partial class PrescriptionLineViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StockInfo))]
    private MedicineOptionDto? medicine;

    [ObservableProperty]
    private decimal? quantity;

    [ObservableProperty]
    private string? dosageInstructions;

    public string StockInfo => Medicine is null
        ? string.Empty
        : $"Tồn: {Display.Qty(Medicine.StockQuantity)} {Medicine.Unit} · {Display.Money(Medicine.UnitPrice)}/{Medicine.Unit}";
}

/// <summary>Một dịch vụ có thể chỉ định (khám, xét nghiệm...), tích chọn bằng ô vuông.</summary>
public sealed partial class ServiceOptionViewModel(ClinicServiceDto dto) : ObservableObject
{
    public int ServiceId => dto.ServiceId;
    public string Label => $"{dto.Name} · {Display.Money(dto.Price)}";

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>
/// Màn hình khám & kê đơn. Danh sách dòng thuốc là ObservableCollection:
/// thêm/xóa dòng trong ViewModel -> bảng trên màn hình tự thêm/xóa dòng (web phải dùng JavaScript).
/// </summary>
public sealed partial class ConsultationViewModel(IServiceRunner runner, INavigator navigator, IDialogService dialogs)
    : PageViewModel
{
    public override string Title => "Khám & kê đơn";

    public int VisitId { get; set; }

    [ObservableProperty]
    private PatientBanner? patient;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaiting))]
    private VisitStatus visitStatus;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    private string? readOnlyReason;

    [ObservableProperty]
    private string? chiefComplaint;

    [ObservableProperty]
    private string? clinicalNotes;

    [ObservableProperty]
    private string? diagnosis;

    [ObservableProperty]
    private string? treatmentPlan;

    [ObservableProperty]
    private bool acknowledgeWarnings;

    [ObservableProperty]
    private string? prescriptionStatusText;

    public bool IsWaiting => VisitStatus == VisitStatus.Waiting;

    public ObservableCollection<MedicineOptionDto> MedicineOptions { get; } = [];
    public ObservableCollection<PrescriptionLineViewModel> Lines { get; } = [];
    public ObservableCollection<ServiceOptionViewModel> Services { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<string> PreviousVisits { get; } = [];
    public bool HasWarnings => Warnings.Count > 0;

    protected override async Task OnLoadAsync()
    {
        var medicines = await runner.RunAsync((IConsultationService s) => s.GetMedicineOptionsAsync());
        var services = await runner.RunAsync((IServiceCatalogService s) => s.ListAsync(false));
        var dto = await runner.RunAsync((IConsultationService s) => s.GetAsync(VisitId));
        if (dto is null)
        {
            ErrorMessage = "Không tìm thấy lượt khám.";
            return;
        }

        MedicineOptions.Clear();
        foreach (var medicine in medicines)
        {
            MedicineOptions.Add(medicine);
        }

        Patient = PatientBanner.From(dto.Patient);
        VisitStatus = dto.VisitStatus;
        CanEdit = dto.CanEdit;
        ReadOnlyReason = dto.ReadOnlyReason;
        ChiefComplaint = dto.ChiefComplaint;
        ClinicalNotes = dto.ClinicalNotes;
        Diagnosis = dto.Diagnosis;
        TreatmentPlan = dto.TreatmentPlan;
        PrescriptionStatusText = dto.PrescriptionStatus is { } ps
            ? "Đơn thuốc: " + ps.ToLabel() + (dto.ReturnReason is null ? string.Empty : " · Lý do trả về: " + dto.ReturnReason)
            : "Chưa có đơn thuốc";

        Lines.Clear();
        foreach (var item in dto.Items)
        {
            Lines.Add(new PrescriptionLineViewModel
            {
                Medicine = MedicineOptions.FirstOrDefault(x => x.MedicineId == item.MedicineId),
                Quantity = item.Quantity,
                DosageInstructions = item.DosageInstructions
            });
        }

        Services.Clear();
        foreach (var service in services)
        {
            Services.Add(new ServiceOptionViewModel(service) { IsSelected = dto.ServiceIds.Contains(service.ServiceId) });
        }

        Warnings.Clear();
        foreach (var warning in dto.Warnings)
        {
            Warnings.Add((warning.Severity == WarningSeverity.Red ? "⛔ " : "⚠ ") + warning.Message);
        }

        PreviousVisits.Clear();
        foreach (var visit in dto.PreviousVisits)
        {
            PreviousVisits.Add($"{Display.Date(visit.StartsAtUtc)} · {visit.DoctorName} · {visit.Diagnosis ?? "(chưa có chẩn đoán)"}");
        }

        OnPropertyChanged(nameof(HasWarnings));
    }

    [RelayCommand]
    private Task StartAsync() => RunBusyAsync(async () =>
    {
        if (Report(await runner.RunAsync((IConsultationService s) => s.StartAsync(VisitId))))
        {
            await OnLoadAsync();
        }
    }, clearSuccess: true);

    [RelayCommand]
    private void AddLine() => Lines.Add(new PrescriptionLineViewModel { Quantity = 1 });

    [RelayCommand]
    private void RemoveLine(PrescriptionLineViewModel? line)
    {
        if (line is not null)
        {
            Lines.Remove(line);
        }
    }

    [RelayCommand]
    private Task SaveDraftAsync() => RunBusyAsync(async () =>
    {
        if (Report(await runner.RunAsync((IConsultationService s) => s.SaveDraftAsync(BuildDto()))))
        {
            await OnLoadAsync(); // nạp lại để thấy cảnh báo dị ứng / tồn kho mới nhất
        }
    }, clearSuccess: true);

    [RelayCommand]
    private Task CompleteAsync() => RunBusyAsync(async () =>
    {
        if (!dialogs.Confirm("Hoàn tất khám",
                "Hoàn tất khám sẽ phát hành đơn thuốc và chuyển hóa đơn sang quầy thu ngân. Tiếp tục?"))
        {
            return;
        }

        var result = await runner.RunAsync((IConsultationService s) => s.CompleteAsync(BuildDto()));
        if (!Report(result))
        {
            await OnLoadAsync(); // Service đã lưu nháp phần vừa nhập; nạp lại để hiện cảnh báo
            ErrorMessage = result.Message;
            return;
        }

        var message = result.Message;
        navigator.NavigateTo<DoctorQueueViewModel>(page => page.SuccessMessage = message);
    }, clearSuccess: true);

    [RelayCommand]
    private void Back() => navigator.NavigateTo<DoctorQueueViewModel>();

    private SaveConsultationDto BuildDto() => new(
        VisitId, ChiefComplaint, ClinicalNotes, Diagnosis, TreatmentPlan,
        Lines.Where(x => x.Medicine is not null && x.Quantity > 0)
            .Select(x => new PrescriptionItemInputDto(x.Medicine!.MedicineId, x.Quantity!.Value, x.DosageInstructions))
            .ToList(),
        Services.Where(x => x.IsSelected).Select(x => x.ServiceId).ToList(),
        AcknowledgeWarnings);
}
