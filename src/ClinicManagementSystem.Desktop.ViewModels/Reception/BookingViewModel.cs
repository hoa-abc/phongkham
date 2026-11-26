using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Reception;

/// <summary>
/// ĐẶT LỊCH KHÁM - trái tim của phần C.
/// Luồng 3 bước: (1) tìm & chọn bệnh nhi -> (2) chọn bác sĩ, ngày, khung giờ -> (3) xác nhận.
///
/// So với bản web: web cần HAI form (GET để tải khung giờ, POST để lưu) vì mỗi lần đổi lựa chọn
/// phải gửi request lên server. Ở MVVM, ViewModel "sống" suốt lúc màn hình mở: đổi bác sĩ/ngày
/// -> hàm OnSelectedDoctorChanged tự chạy -> tải lại lưới giờ. Không cần form, không tải lại trang.
/// </summary>
public sealed partial class BookingViewModel(IServiceRunner runner, INavigator navigator) : PageViewModel
{
    public const int SlotMinutes = 30;

    public override string Title => "Đặt lịch khám";
    public override string? Subtitle => "Chọn đúng hồ sơ bé, bác sĩ và một khung giờ còn trống.";

    /// <summary>Mở từ trang hồ sơ bệnh nhi ("Đặt lịch cho bé này") thì chọn sẵn bé.</summary>
    public int? PreselectedPatientId { get; set; }

    // ---------- Bước 1: bệnh nhi ----------
    [ObservableProperty]
    private string? patientKeyword;

    public ObservableCollection<PatientRow> Patients { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPatient))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private PatientRow? selectedPatient;

    public bool HasPatient => SelectedPatient is not null;

    // ---------- Bước 2: bác sĩ, ngày, khung giờ ----------
    public ObservableCollection<DoctorOptionDto> Doctors { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private DoctorOptionDto? selectedDoctor;

    /// <summary>DateTime? vì DatePicker của WPF làm việc với DateTime; ta chỉ dùng phần ngày.</summary>
    [ObservableProperty]
    private DateTime? selectedDate = ClinicClock.Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>Không cho chọn ngày trong quá khứ trên DatePicker.</summary>
    public DateTime MinDate => ClinicClock.Today.ToDateTime(TimeOnly.MinValue);

    public ObservableCollection<SlotItem> Slots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryTime))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private SlotItem? selectedSlot;

    [ObservableProperty]
    private string? reason;

    public string SummaryTime => SelectedSlot is null || SelectedDate is null
        ? "Chưa chọn khung giờ"
        : $"{SelectedSlot.Label} · {SelectedDate.Value:dd/MM/yyyy} (30 phút)";

    // ---------- Nạp dữ liệu ----------
    protected override async Task OnLoadAsync()
    {
        var doctors = await runner.RunAsync((IUserService s) => s.GetDoctorsAsync());
        Doctors.Clear();
        foreach (var doctor in doctors)
        {
            Doctors.Add(doctor);
        }

        await LoadPatientsAsync();
        if (PreselectedPatientId is { } id)
        {
            SelectedPatient = Patients.FirstOrDefault(x => x.PatientId == id);
        }

        SelectedDoctor ??= Doctors.FirstOrDefault(); // đổi bác sĩ -> OnSelectedDoctorChanged tải lưới giờ
    }

    [RelayCommand]
    private Task SearchPatientsAsync() => RunBusyAsync(LoadPatientsAsync);

    private async Task LoadPatientsAsync()
    {
        var keepId = SelectedPatient?.PatientId;
        var items = await runner.RunAsync((IPatientService s) => s.SearchAsync(PatientKeyword));
        Patients.Clear();
        foreach (var item in items)
        {
            Patients.Add(new PatientRow(item));
        }

        SelectedPatient = Patients.FirstOrDefault(x => x.PatientId == keepId)
                          ?? (Patients.Count == 1 ? Patients[0] : null); // tìm ra đúng 1 bé thì chọn luôn
    }

    // Hai hàm "partial" dưới đây do CommunityToolkit gọi TỰ ĐỘNG khi thuộc tính đổi.
    partial void OnSelectedDoctorChanged(DoctorOptionDto? value) => LoadSlotsCommand.Execute(null);

    partial void OnSelectedDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(SummaryTime));
        LoadSlotsCommand.Execute(null);
    }

    [RelayCommand]
    private Task LoadSlotsAsync() => RunBusyAsync(ReloadSlotsAsync);

    private int slotRequestVersion;

    private async Task ReloadSlotsAsync()
    {
        // Người dùng đổi bác sĩ/ngày liên tục -> nhiều lần tải chồng nhau. Đánh số từng lần tải,
        // chỉ lần MỚI NHẤT được ghi vào lưới (tránh lưới bị lẫn khung giờ của bác sĩ cũ).
        var version = ++slotRequestVersion;
        SelectedSlot = null;
        if (SelectedDoctor is null || SelectedDate is null)
        {
            Slots.Clear();
            return;
        }

        var date = DateOnly.FromDateTime(SelectedDate.Value);
        var slots = await runner.RunAsync((IAppointmentService s) => s.GetSlotsAsync(SelectedDoctor.UserId, date));
        if (version != slotRequestVersion)
        {
            return;
        }

        Slots.Clear();
        foreach (var slot in slots)
        {
            Slots.Add(new SlotItem(slot));
        }
    }

    // ---------- Bước 3: xác nhận ----------
    /// <summary>
    /// CanExecute: nút "Xác nhận lịch" chỉ sáng khi đã chọn đủ bé + bác sĩ + một khung CÒN TRỐNG.
    /// Đây chỉ là giúp người dùng ít sai. Service vẫn kiểm tra lại toàn bộ (ba lớp chống trùng).
    /// </summary>
    private bool CanConfirm() => SelectedPatient is not null && SelectedDoctor is not null
                                 && SelectedSlot is { IsAvailable: true } && SelectedDate is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private Task ConfirmAsync() => RunBusyAsync(async () =>
    {
        // Chụp lại lựa chọn hiện tại vào biến cục bộ: trong lúc chờ Service, người dùng có thể bấm chọn thứ khác.
        var date = DateOnly.FromDateTime(SelectedDate!.Value);
        var slot = SelectedSlot!;
        var doctor = SelectedDoctor!;
        var patient = SelectedPatient!;
        var startUtc = ClinicClock.ToUtc(date.ToDateTime(slot.Start)); // giờ VN -> UTC trước khi gửi Service
        var result = await runner.RunAsync((IAppointmentService s) => s.CreateAppointmentAsync(
            new CreateAppointmentDto(patient.PatientId, doctor.UserId, startUtc,
                startUtc.AddMinutes(SlotMinutes), Reason)));

        if (result.Succeeded)
        {
            SuccessMessage = $"{result.Message} {patient.FullName} ({patient.MedicalRecordNumber}) lúc " +
                             $"{slot.Label} ngày {date:dd/MM/yyyy} với {doctor.DisplayName}.";
            Reason = null;
        }
        else
        {
            // Ví dụ: quầy khác vừa đặt mất khung này. Giữ bé/bác sĩ/ngày, chỉ bỏ khung giờ và tải lưới MỚI NHẤT.
            ErrorMessage = result.Message;
        }

        await ReloadSlotsAsync(); // thành công hay thất bại đều tải lại để thấy khung vừa bị chiếm
    }, clearSuccess: true);

    [RelayCommand]
    private void OpenAppointmentList() => navigator.NavigateTo<AppointmentListViewModel>(page =>
        page.SelectedDate = SelectedDate ?? page.SelectedDate);

    [RelayCommand]
    private void CreatePatient() => navigator.NavigateTo<CreatePatientViewModel>();
}
