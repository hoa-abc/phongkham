using System.Windows;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;

namespace ClinicManagementSystem.Desktop.Services;

/// <summary>
/// Bản cài đặt THẬT của IDialogService bằng WPF. ViewModel chỉ biết interface;
/// khi kiểm thử, ta đưa vào một bản giả luôn trả "Đồng ý" mà không hiện hộp thoại nào.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    public bool Confirm(string title, string message)
        => MessageBox.Show(Application.Current.MainWindow!, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? AskText(string title, string prompt)
    {
        var dialog = new TextPromptWindow(title, prompt) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }
}
