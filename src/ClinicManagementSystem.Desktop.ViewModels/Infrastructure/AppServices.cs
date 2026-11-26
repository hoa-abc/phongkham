using System.ComponentModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.Desktop.ViewModels.Infrastructure;

// =====================================================================================
// Các "dịch vụ của tầng giao diện" mà ViewModel cần. Tất cả đều là INTERFACE:
// ViewModel chỉ biết hợp đồng, còn bản cài đặt thật (WPF) hoặc bản giả (test) do DI đưa vào.
// =====================================================================================

/// <summary>
/// Gọi một Service nghiệp vụ trong một DI scope RIÊNG cho mỗi thao tác.
///
/// Vì sao cần? Service dùng ClinicDbContext (vòng đời Scoped). Trên web, mỗi HTTP request là một scope.
/// App desktop không có request, mà cửa sổ thì mở hàng giờ. Nếu giữ MỘT DbContext suốt buổi:
///  - dữ liệu bị "cũ" (DbContext nhớ entity đã đọc, không thấy thay đổi của quầy khác),
///  - hai thao tác chạy cùng lúc trên một DbContext sẽ lỗi (DbContext không an toàn đa luồng).
/// Giải pháp: mỗi lần bấm nút = một scope mới = một DbContext mới, xong thì hủy. Giống hệt "một request".
/// </summary>
public interface IServiceRunner
{
    /// <example>var list = await runner.RunAsync((IPatientService s) => s.SearchAsync(keyword));</example>
    Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull;
}

public sealed class ScopedServiceRunner(IServiceScopeFactory scopeFactory) : IServiceRunner
{
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await work(service);
    }
}

/// <summary>Hộp thoại hỏi lại / nhập lý do. ViewModel KHÔNG gọi MessageBox trực tiếp (MessageBox là đồ của WPF).</summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    /// <summary>Hỏi người dùng nhập một dòng chữ (ví dụ lý do hủy). Trả null nếu người dùng bấm Hủy.</summary>
    string? AskText(string title, string prompt);
}

/// <summary>Chuyển trang trong vùng nội dung của cửa sổ chính.</summary>
public interface INavigator : INotifyPropertyChanged
{
    PageViewModel? CurrentPage { get; }

    /// <summary>Mở trang TPage. configure dùng để truyền tham số, ví dụ mã lịch hẹn cần check-in.</summary>
    void NavigateTo<TPage>(Action<TPage>? configure = null) where TPage : PageViewModel;
}

public sealed partial class Navigator(IServiceProvider services) : ObservableObject, INavigator
{
    [ObservableProperty]
    private PageViewModel? currentPage;

    public void NavigateTo<TPage>(Action<TPage>? configure = null) where TPage : PageViewModel
    {
        // Trang đăng ký Transient: mỗi lần mở là một ViewModel mới, dữ liệu tươi.
        var page = services.GetRequiredService<TPage>();
        configure?.Invoke(page);
        CurrentPage = page;
        page.LoadCommand.Execute(null);
    }
}

/// <summary>
/// Phiên đăng nhập của app desktop - tương đương cookie đăng nhập bên web.
/// Đồng thời là ICurrentUserContext để tầng Services biết "ai đang thao tác" (kiểm quyền, ghi nhật ký).
/// </summary>
public sealed class UserSession : ICurrentUserContext
{
    public int? UserId { get; private set; }
    public UserRole? Role { get; private set; }
    public string? DisplayName { get; private set; }
    public string? UserName { get; private set; }
    public Guid? CorrelationId { get; private set; }

    public bool IsSignedIn => UserId is not null;

    /// <summary>Bắn ra khi đăng nhập / đăng xuất để cửa sổ chính đổi menu.</summary>
    public event EventHandler? Changed;

    public void SignIn(AuthenticatedUserDto user)
    {
        UserId = user.UserId;
        Role = user.Role;
        DisplayName = user.DisplayName;
        UserName = user.UserName;
        CorrelationId = Guid.NewGuid();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        UserId = null;
        Role = null;
        DisplayName = null;
        UserName = null;
        CorrelationId = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
