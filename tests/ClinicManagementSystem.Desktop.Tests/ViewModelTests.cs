using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Desktop.ViewModels.Reception;
using ClinicManagementSystem.Desktop.ViewModels.Shell;

namespace ClinicManagementSystem.Desktop.Tests;

/// <summary>UNIT TEST cho ViewModel - phần C (đặt lịch, check-in) và phần A (đăng nhập, menu).</summary>
public sealed class BookingViewModelTests
{
    private readonly FakeAppointmentService appointments = new();
    private readonly FakeNavigator navigator = new();

    private BookingViewModel CreateViewModel(int? preselectedPatientId = null)
    {
        var runner = new FakeServiceRunner()
            .Add<Services.Contracts.IUserService>(new FakeUserService())
            .Add<Services.Contracts.IPatientService>(new FakePatientService())
            .Add<Services.Contracts.IAppointmentService>(appointments);
        return new BookingViewModel(runner, navigator) { PreselectedPatientId = preselectedPatientId };
    }

    [Fact]
    public async Task Mo_trang_thi_nap_bac_si_benh_nhi_va_tu_tai_luoi_khung_gio_cua_bac_si_dau_tien()
    {
        var vm = CreateViewModel(preselectedPatientId: 2);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Doctors.Count);
        Assert.Equal(FakeUserService.DoctorA, vm.SelectedDoctor);
        Assert.Equal("BN-00002", vm.SelectedPatient?.MedicalRecordNumber); // chọn sẵn bé được truyền sang
        Assert.Equal(["08:00", "08:30", "09:00"], vm.Slots.Select(x => x.Label));
        Assert.False(vm.Slots[1].IsAvailable);
        Assert.Equal("Đã đặt", vm.Slots[1].Note);
    }

    [Fact]
    public async Task Doi_bac_si_hoac_ngay_thi_luoi_khung_gio_tu_tai_lai_va_bo_khung_dang_chon()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedSlot = vm.Slots[0];
        var before = appointments.SlotRequests;

        vm.SelectedDoctor = FakeUserService.DoctorB; // không gọi lệnh nào: hàm OnSelectedDoctorChanged tự chạy
        vm.SelectedDate = TestDates.Tomorrow;

        Assert.Equal(before + 2, appointments.SlotRequests);
        Assert.Null(vm.SelectedSlot);
    }

    [Fact]
    public async Task Nut_xac_nhan_chi_bat_khi_da_chon_du_be_bac_si_va_khung_con_trong()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.ConfirmCommand.CanExecute(null)); // chưa chọn bé

        vm.SelectedPatient = vm.Patients[0];
        vm.SelectedSlot = vm.Slots[1]; // khung "Đã đặt"
        Assert.False(vm.ConfirmCommand.CanExecute(null));

        vm.SelectedSlot = vm.Slots[0];
        Assert.True(vm.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Xac_nhan_thanh_cong_gui_gio_UTC_dung_va_tai_lai_luoi_gio()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedDate = TestDates.Tomorrow;
        vm.SelectedPatient = vm.Patients[0];
        vm.SelectedSlot = vm.Slots[0]; // 08:00 giờ Việt Nam
        vm.Reason = "Sốt, ho 2 ngày";
        var before = appointments.SlotRequests;

        await vm.ConfirmCommand.ExecuteAsync(null);

        var sent = Assert.Single(appointments.Created);
        var expectedStart = ClinicClock.ToUtc(TestDates.Tomorrow.AddHours(8));
        Assert.Equal(expectedStart, sent.StartsAtUtc);              // 08:00 VN = 01:00 UTC
        Assert.Equal(expectedStart.AddMinutes(30), sent.EndsAtUtc);
        Assert.Equal(1, sent.PatientId);
        Assert.Equal(10, sent.DoctorId);
        Assert.Contains("Đặt lịch thành công", vm.SuccessMessage);
        Assert.Contains("BN-00001", vm.SuccessMessage);               // thông báo nêu rõ mã bé
        Assert.False(vm.HasError);
        Assert.Null(vm.Reason);
        Assert.Equal(before + 1, appointments.SlotRequests);         // lưới giờ được tải lại
    }

    [Fact]
    public async Task Khung_gio_vua_bi_quay_khac_dat_thi_bao_loi_giu_lua_chon_va_bo_khung_cu()
    {
        appointments.NextResult = ServiceResult<AppointmentDto>.Failure(
            "APPOINTMENT_SLOT_TAKEN", "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.");
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedPatient = vm.Patients[0];
        vm.SelectedSlot = vm.Slots[2];

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.", vm.ErrorMessage);
        Assert.Null(vm.SuccessMessage);
        Assert.NotNull(vm.SelectedPatient);   // không bắt tiếp tân chọn lại bé
        Assert.NotNull(vm.SelectedDoctor);
        Assert.Null(vm.SelectedSlot);         // khung cũ đã mất -> phải chọn khung khác
    }

    [Fact]
    public async Task Tim_ra_dung_mot_be_thi_tu_chon_be_do()
    {
        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.PatientKeyword = "BN-00002";
        await vm.SearchPatientsCommand.ExecuteAsync(null);

        Assert.Single(vm.Patients);
        Assert.Equal("Trần Bảo Ngọc", vm.SelectedPatient?.FullName);
    }
}

public sealed class CheckInViewModelTests
{
    [Fact]
    public async Task Bo_trong_chi_so_thi_bao_loi_va_khong_goi_service()
    {
        var reception = new FakeReceptionService();
        var navigator = new FakeNavigator();
        var vm = new CheckInViewModel(new FakeServiceRunner().Add<Services.Contracts.IReceptionService>(reception), navigator)
        {
            AppointmentId = 7
        };
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.True(vm.HasErrors);
        Assert.Contains(vm.GetErrors(nameof(vm.WeightKg)), e => e.ErrorMessage == "Vui lòng nhập cân nặng.");
        Assert.Empty(reception.CheckIns);
        Assert.Empty(navigator.Visited);
        Assert.Equal("BN-00001", vm.Patient?.MedicalRecordNumber); // dải nhận diện bé đã hiện
        Assert.True(vm.Patient?.HasAllergies);
    }

    [Fact]
    public async Task Nhap_dung_thi_check_in_va_quay_ve_hang_cho_con_sot_thi_hien_canh_bao()
    {
        var reception = new FakeReceptionService();
        var navigator = new FakeNavigator();
        var vm = new CheckInViewModel(new FakeServiceRunner().Add<Services.Contracts.IReceptionService>(reception), navigator)
        {
            AppointmentId = 7
        };
        await vm.LoadCommand.ExecuteAsync(null);

        vm.WeightKg = 15.5m;
        vm.HeightCm = 98;
        vm.HeartRate = 110;
        vm.TemperatureC = 38.4m;
        Assert.True(vm.IsFever);
        vm.TemperatureC = 50; // ngoài khoảng -> lỗi ngay khi gán
        Assert.Contains(vm.GetErrors(nameof(vm.TemperatureC)), e => e.ErrorMessage!.Contains("30 đến 45"));
        vm.TemperatureC = 38.4m;

        await vm.ConfirmCommand.ExecuteAsync(null);

        var sent = Assert.Single(reception.CheckIns);
        Assert.Equal(7, sent.AppointmentId);
        Assert.Equal(38.4m, sent.TemperatureC);
        Assert.Equal("Sốt", sent.Symptoms); // lấy sẵn lý do khám làm triệu chứng
        Assert.Equal([typeof(CheckInQueueViewModel)], navigator.Visited);
    }
}

public sealed class ShellAndLoginTests
{
    [Theory]
    [InlineData(UserRole.Receptionist, "Đặt lịch khám")]
    [InlineData(UserRole.Doctor, "Hàng đợi khám")]
    [InlineData(UserRole.Pharmacist, "Đơn chờ cấp")]
    [InlineData(UserRole.Admin, "Người dùng")]
    public void Menu_doi_theo_vai_tro(UserRole role, string expected)
    {
        var titles = ShellViewModel.MenuFor(role).Select(x => x.Title).ToList();
        Assert.Equal("Tổng quan", titles[0]);
        Assert.Contains(expected, titles);
    }

    [Fact]
    public void Bac_si_khong_thay_menu_dat_lich_hay_thanh_toan()
    {
        var titles = ShellViewModel.MenuFor(UserRole.Doctor).Select(x => x.Title).ToList();
        Assert.DoesNotContain("Đặt lịch khám", titles);
        Assert.DoesNotContain("Thanh toán", titles);
    }

    [Fact]
    public async Task Dang_nhap_sai_bao_loi_dung_thi_menu_tiep_tan_hien_ra_va_mo_trang_tong_quan()
    {
        var session = new UserSession();
        var navigator = new FakeNavigator();
        var shell = new ShellViewModel(navigator, session);
        var login = new LoginViewModel(new FakeServiceRunner().Add<Services.Contracts.IAuthService>(new FakeAuthService()), session)
        {
            UserName = "letan",
            Password = "sai"
        };

        await login.SignInCommand.ExecuteAsync(null);
        Assert.Equal("Sai tên đăng nhập hoặc mật khẩu.", login.ErrorMessage);
        Assert.False(shell.IsSignedIn);

        login.Password = "dung-mat-khau";
        await login.SignInCommand.ExecuteAsync(null);

        Assert.True(shell.IsSignedIn);
        Assert.Equal("Tiếp tân Demo", shell.UserDisplayName);
        Assert.Contains(shell.MenuItems, x => x.Title == "Đặt lịch khám");
        Assert.Equal(typeof(DashboardViewModel), navigator.Visited.Last());
        Assert.Equal(string.Empty, login.Password); // không giữ mật khẩu trong bộ nhớ sau khi đăng nhập

        shell.SignOutCommand.Execute(null);
        Assert.False(shell.IsSignedIn);
        Assert.Empty(shell.MenuItems);
        Assert.Equal(typeof(LoginViewModel), navigator.Visited.Last());
    }

    [Fact]
    public void Nut_dang_nhap_mo_di_khi_chua_nhap_ten()
    {
        var login = new LoginViewModel(new FakeServiceRunner(), new UserSession());
        Assert.False(login.SignInCommand.CanExecute(null));
        login.UserName = "letan";
        Assert.True(login.SignInCommand.CanExecute(null));
    }
}
