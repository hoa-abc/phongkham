using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Reception;

/// <summary>Tra cứu hồ sơ bệnh nhi theo mã / tên bé / tên hoặc SĐT phụ huynh.</summary>
public sealed partial class PatientListViewModel(IServiceRunner runner, INavigator navigator, UserSession session)
    : PageViewModel
{
    public override string Title => "Hồ sơ bệnh nhi";
    public override string? Subtitle => "Tìm theo mã bệnh nhi, tên bé, tên hoặc số điện thoại phụ huynh.";

    [ObservableProperty]
    private string? keyword;

    public ObservableCollection<PatientRow> Items { get; } = [];

    /// <summary>Chỉ tiếp tân/admin mới đặt lịch và tạo hồ sơ (bác sĩ chỉ tra cứu).</summary>
    public bool CanManage => session.Role is UserRole.Receptionist or UserRole.Admin;

    protected override async Task OnLoadAsync()
    {
        var items = await runner.RunAsync((IPatientService s) => s.SearchAsync(Keyword));
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new PatientRow(item));
        }
    }

    [RelayCommand]
    private void BookFor(PatientRow? row)
    {
        if (row is not null)
        {
            navigator.NavigateTo<BookingViewModel>(page => page.PreselectedPatientId = row.PatientId);
        }
    }

    [RelayCommand]
    private void NewPatient() => navigator.NavigateTo<CreatePatientViewModel>();
}

/// <summary>
/// Tạo hồ sơ bệnh nhi + người giám hộ (+ dị ứng nếu có). Tạo xong chuyển thẳng sang đặt lịch cho bé đó.
/// Người giám hộ được nhận diện theo SỐ ĐIỆN THOẠI: đã có thì hồ sơ mới tự liên kết vào phụ huynh cũ.
/// </summary>
public sealed partial class CreatePatientViewModel(IServiceRunner runner, INavigator navigator) : PageViewModel
{
    public override string Title => "Tạo hồ sơ bệnh nhi";
    public override string? Subtitle => "Thông tin bé, người giám hộ và dị ứng đã biết.";

    public IReadOnlyList<Option<Gender>> GenderOptions { get; } =
        [new(Gender.Male, Gender.Male.ToLabel()), new(Gender.Female, Gender.Female.ToLabel()), new(Gender.Other, Gender.Other.ToLabel())];

    public IReadOnlyList<Option<GuardianRelationship>> RelationshipOptions { get; } =
        Enum.GetValues<GuardianRelationship>().Select(x => new Option<GuardianRelationship>(x, x.ToLabel())).ToList();

    public IReadOnlyList<Option<AllergySeverity>> SeverityOptions { get; } =
        Enum.GetValues<AllergySeverity>().Select(x => new Option<AllergySeverity>(x, x.ToLabel())).ToList();

    public DateTime MaxBirthDate => ClinicClock.Today.ToDateTime(TimeOnly.MinValue);

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng nhập họ tên bé.")]
    [StringLength(160)]
    private string? fullName;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    private DateTime? dateOfBirth;

    [ObservableProperty]
    private Option<Gender>? selectedGender;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại phụ huynh.")]
    [RegularExpression(@"^[\d\s.]{9,14}$", ErrorMessage = "Số điện thoại gồm 9-11 chữ số.")]
    private string? guardianPhone;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Vui lòng nhập họ tên người giám hộ.")]
    [StringLength(160)]
    private string? guardianName;

    [ObservableProperty]
    private Option<GuardianRelationship>? selectedRelationship;

    [ObservableProperty]
    private string? guardianAddress;

    [ObservableProperty]
    private string? allergyName;

    [ObservableProperty]
    private Option<AllergySeverity>? selectedSeverity;

    [ObservableProperty]
    private string? allergyReaction;

    /// <summary>Báo ngay "SĐT này đã có hồ sơ phụ huynh" khi gõ đủ số (giống tính năng tra SĐT bên web).</summary>
    [ObservableProperty]
    private string? guardianNote;

    protected override Task OnLoadAsync()
    {
        SelectedGender ??= GenderOptions[0];
        SelectedRelationship ??= RelationshipOptions[0];
        SelectedSeverity ??= SeverityOptions.FirstOrDefault(x => x.Value == AllergySeverity.Moderate);
        return Task.CompletedTask;
    }

    partial void OnGuardianPhoneChanged(string? value) => LookupGuardianCommand.Execute(null);

    [RelayCommand]
    private async Task LookupGuardianAsync()
    {
        var digits = CodeFormatter.NormalizePhone(GuardianPhone);
        if (digits.Length < 9)
        {
            GuardianNote = null;
            return;
        }

        try
        {
            var guardian = await runner.RunAsync((IPatientService s) => s.FindGuardianByPhoneAsync(digits));
            GuardianNote = guardian is null
                ? "Chưa có hồ sơ phụ huynh với số này - hệ thống sẽ tạo mới."
                : $"Đã có hồ sơ phụ huynh: {guardian.FullName} ({guardian.PatientCount} bé). Hồ sơ mới sẽ được liên kết vào phụ huynh này.";
            if (guardian is not null && string.IsNullOrWhiteSpace(GuardianName))
            {
                GuardianName = guardian.FullName;
            }
        }
        catch (Exception)
        {
            GuardianNote = null; // tra cứu chỉ để tiện tay; lỗi thì bỏ qua, Service vẫn tự liên kết khi lưu
        }
    }

    [RelayCommand]
    private Task SaveAsync() => RunBusyAsync(async () =>
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Vui lòng điền đủ các ô được viền đỏ.";
            return;
        }

        var dto = new CreatePatientDto(
            FullName!.Trim(), DateOnly.FromDateTime(DateOfBirth!.Value), SelectedGender?.Value ?? Gender.Unknown,
            GuardianPhone!, GuardianName!.Trim(), SelectedRelationship?.Value ?? GuardianRelationship.Parent,
            GuardianAddress, AllergyName, null, SelectedSeverity?.Value ?? AllergySeverity.Unknown, AllergyReaction);
        var result = await runner.RunAsync((IPatientService s) => s.CreateAsync(dto));
        if (!Report(result))
        {
            return;
        }

        // Tạo hồ sơ xong -> sang đặt lịch, chọn sẵn bé vừa tạo. Luồng tiếp tân không bị ngắt quãng.
        var patientId = result.Value;
        navigator.NavigateTo<BookingViewModel>(page =>
        {
            page.PreselectedPatientId = patientId;
            page.SuccessMessage = result.Message;
        });
    }, clearSuccess: true);

    [RelayCommand]
    private void Cancel() => navigator.NavigateTo<PatientListViewModel>();
}
