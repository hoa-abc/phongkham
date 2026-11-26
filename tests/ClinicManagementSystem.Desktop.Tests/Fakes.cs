using System.ComponentModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Results;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;

namespace ClinicManagementSystem.Desktop.Tests;

// =====================================================================================
// Đồ GIẢ cho unit test ViewModel: không database, không cửa sổ, chạy được trên mọi máy.
// Đây chính là lợi ích lớn nhất của MVVM + interface: ViewModel test được mà không cần giao diện.
// =====================================================================================

/// <summary>Thay cho ScopedServiceRunner: đưa thẳng service giả cho ViewModel.</summary>
public sealed class FakeServiceRunner : IServiceRunner
{
    private readonly Dictionary<Type, object> services = [];

    public FakeServiceRunner Add<T>(T service) where T : notnull
    {
        services[typeof(T)] = service;
        return this;
    }

    public Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull
        => work((TService)services[typeof(TService)]);
}

/// <summary>Ghi lại các lần chuyển trang thay vì mở trang thật.</summary>
public sealed class FakeNavigator : INavigator
{
    public List<Type> Visited { get; } = [];
    public PageViewModel? CurrentPage => null;

    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

    public void NavigateTo<TPage>(Action<TPage>? configure = null) where TPage : PageViewModel => Visited.Add(typeof(TPage));
}

/// <summary>Hộp thoại giả: trả lời theo kịch bản, không hiện gì.</summary>
public sealed class FakeDialogs(bool confirm = true, string? text = "Lý do kiểm thử") : IDialogService
{
    public bool Confirm(string title, string message) => confirm;
    public string? AskText(string title, string prompt) => text;
}

public sealed class FakeUserService : IUserService
{
    public static readonly DoctorOptionDto DoctorA = new(10, "BS. Demo Một", "Nhi Tổng quát - Phòng 1");
    public static readonly DoctorOptionDto DoctorB = new(11, "BS. Demo Hai", "Tai Mũi Họng - Phòng 2");

    public Task<IReadOnlyList<DoctorOptionDto>> GetDoctorsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DoctorOptionDto>>([DoctorA, DoctorB]);

    public Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserListItemDto?> GetAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult<int>> SaveAsync(SaveUserDto dto, CancellationToken ct = default) => throw new NotImplementedException();
}

public sealed class FakePatientService : IPatientService
{
    public static readonly PatientListItemDto An = new(1, "BN-00001", "Nguyễn Minh An", new DateOnly(2022, 3, 1), Gender.Male,
        1, "Nguyễn Văn Hùng", "0901000001", ["Amoxicillin"]);
    public static readonly PatientListItemDto Ngoc = new(2, "BN-00002", "Trần Bảo Ngọc", new DateOnly(2021, 7, 9), Gender.Female,
        2, "Trần Thị Lan", "0901000002", []);

    public string? LastKeyword { get; private set; }

    public Task<IReadOnlyList<PatientListItemDto>> SearchAsync(string? keyword, CancellationToken ct = default)
    {
        LastKeyword = keyword;
        IReadOnlyList<PatientListItemDto> all = [An, Ngoc];
        return Task.FromResult<IReadOnlyList<PatientListItemDto>>(string.IsNullOrWhiteSpace(keyword)
            ? all
            : all.Where(x => x.FullName.Contains(keyword) || x.MedicalRecordNumber.Contains(keyword)).ToList());
    }

    public Task<PatientDetailDto?> GetAsync(int patientId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult<int>> CreateAsync(CreatePatientDto dto, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult> UpdateAsync(UpdatePatientDto dto, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult> AddAllergyAsync(AddAllergyDto dto, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult> DeactivateAllergyAsync(int allergyId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<GuardianListItemDto>> SearchGuardiansAsync(string? keyword, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<GuardianDetailDto?> GetGuardianAsync(int guardianId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<GuardianListItemDto?> FindGuardianByPhoneAsync(string phone, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult<int>> SaveGuardianAsync(SaveGuardianDto dto, CancellationToken ct = default) => throw new NotImplementedException();
}

/// <summary>Lịch giả: 08:00 trống, 08:30 đã đặt, 09:00 trống. Ghi lại lần đặt lịch để test kiểm tra.</summary>
public sealed class FakeAppointmentService : IAppointmentService
{
    public List<CreateAppointmentDto> Created { get; } = [];
    public int SlotRequests { get; private set; }

    /// <summary>Kết quả trả về khi đặt lịch (mặc định: thành công).</summary>
    public ServiceResult<AppointmentDto> NextResult { get; set; } =
        ServiceResult<AppointmentDto>.Success(new AppointmentDto(99, 1, 10, DateTime.UtcNow, DateTime.UtcNow, AppointmentStatus.Booked), "Đặt lịch thành công.");

    public Task<IReadOnlyList<SlotDto>> GetSlotsAsync(int doctorId, DateOnly date, CancellationToken ct = default)
    {
        SlotRequests++;
        SlotDto Slot(int h, int m, string? reason) => new(date.ToDateTime(new TimeOnly(h, m)), date.ToDateTime(new TimeOnly(h, m)).AddMinutes(30), reason is null, reason);
        return Task.FromResult<IReadOnlyList<SlotDto>>([Slot(8, 0, null), Slot(8, 30, "Đã đặt"), Slot(9, 0, null)]);
    }

    public Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(CreateAppointmentDto dto, CancellationToken ct = default)
    {
        Created.Add(dto);
        return Task.FromResult(NextResult);
    }

    public Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(AppointmentFilterDto filter, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ServiceResult> CancelAsync(int appointmentId, string? reason, CancellationToken ct = default) => throw new NotImplementedException();
}

public sealed class FakeReceptionService : IReceptionService
{
    public List<CheckInDto> CheckIns { get; } = [];

    public Task<CheckInContextDto?> GetCheckInContextAsync(int appointmentId, CancellationToken ct = default)
    {
        var p = FakePatientService.An;
        var appointment = new AppointmentListItemDto(appointmentId, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), AppointmentStatus.Booked,
            p.PatientId, p.MedicalRecordNumber, p.FullName, p.GuardianName, p.GuardianPhone, 10, "BS. Demo Một", null, "Sốt");
        var context = new PatientContextDto(p.PatientId, p.MedicalRecordNumber, p.FullName, p.DateOfBirth, p.Gender,
            p.GuardianName, p.GuardianPhone, null, null, null, null, p.Allergies);
        return Task.FromResult<CheckInContextDto?>(new CheckInContextDto(appointment, context));
    }

    public Task<ServiceResult<int>> CheckInAsync(CheckInDto dto, CancellationToken ct = default)
    {
        CheckIns.Add(dto);
        return Task.FromResult(ServiceResult<int>.Success(5, "Check-in thành công."));
    }

    public Task<IReadOnlyList<AppointmentListItemDto>> GetQueueAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppointmentListItemDto>>([]);
}

public sealed class FakeAuthService : IAuthService
{
    public Task<ServiceResult<AuthenticatedUserDto>> SignInAsync(string userName, string password, CancellationToken ct = default)
        => Task.FromResult(userName == "letan" && password == "dung-mat-khau"
            ? ServiceResult<AuthenticatedUserDto>.Success(new AuthenticatedUserDto(2, "letan", "Tiếp tân Demo", UserRole.Receptionist))
            : ServiceResult<AuthenticatedUserDto>.Failure("LOGIN_FAILED", "Sai tên đăng nhập hoặc mật khẩu."));

    public Task<bool> IsUserActiveAsync(int userId, CancellationToken ct = default) => Task.FromResult(true);
}

public static class TestDates
{
    public static DateTime Tomorrow => ClinicClock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
}
