using ClinicManagementSystem.Common.Results;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClinicManagementSystem.Desktop.ViewModels.Infrastructure;

/// <summary>
/// Lớp cha của mọi ViewModel.
///
/// ObservableValidator (của CommunityToolkit.Mvvm) cho ta 2 thứ:
///  1. INotifyPropertyChanged: đổi thuộc tính -> màn hình WPF tự cập nhật (data binding).
///  2. INotifyDataErrorInfo: kiểm tra [Required], [Range]... -> ô nhập sai tự viền đỏ.
///
/// [ObservableProperty] trên một field (ví dụ isBusy) -> bộ sinh mã tự tạo thuộc tính IsBusy
/// có báo thay đổi. Ta không phải viết tay hàng chục dòng OnPropertyChanged.
/// </summary>
public abstract partial class ViewModelBase : ObservableValidator
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string? successMessage;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    /// <summary>
    /// Chạy một việc bất đồng bộ: bật "đang xử lý", bắt lỗi để app KHÔNG bị tắt đột ngột,
    /// và luôn tắt "đang xử lý" ở cuối (finally).
    /// </summary>
    /// <param name="clearSuccess">true khi người dùng vừa bấm một thao tác mới -> xóa thông báo xanh cũ.</param>
    protected async Task RunBusyAsync(Func<Task> work, bool clearSuccess = false)
    {
        ErrorMessage = null;
        if (clearSuccess)
        {
            SuccessMessage = null;
        }

        IsBusy = true;
        try
        {
            await work();
        }
        catch (UnauthorizedAccessException exception)
        {
            // PermissionGuard của tầng Services ném lỗi này khi sai vai trò.
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            // Thường là không kết nối được SQL Server. Báo lỗi dễ hiểu thay vì làm sập app.
            ErrorMessage = "Không thực hiện được thao tác: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Đưa kết quả của Service ra màn hình: thành công -> thông báo xanh, thất bại -> đỏ.</summary>
    protected bool Report(ServiceResult result)
    {
        SuccessMessage = result.Succeeded ? result.Message : null;
        ErrorMessage = result.Succeeded ? null : result.Message;
        return result.Succeeded;
    }

    protected bool Report<T>(ServiceResult<T> result)
        => Report(new ServiceResult(result.Succeeded, result.ErrorCode, result.Message));
}

/// <summary>
/// Một "trang" hiển thị trong vùng nội dung của cửa sổ chính (lịch hẹn, đặt lịch, hàng đợi khám...).
/// Mỗi trang có tiêu đề và lệnh LoadCommand để nạp dữ liệu khi được mở.
/// </summary>
public abstract partial class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }

    public virtual string? Subtitle => null;

    /// <summary>[RelayCommand] sinh ra thuộc tính LoadCommand (IAsyncRelayCommand) mà View hoặc Navigator gọi.</summary>
    [RelayCommand]
    private Task LoadAsync() => RunBusyAsync(OnLoadAsync);

    /// <summary>Trang con ghi đè hàm này để nạp dữ liệu của mình.</summary>
    protected virtual Task OnLoadAsync() => Task.CompletedTask;
}
