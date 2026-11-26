using System.Windows;
using System.Windows.Controls;
using ClinicManagementSystem.Desktop.ViewModels.Shell;

namespace ClinicManagementSystem.Desktop.Views.Shell;

public partial class LoginView : UserControl
{
    public LoginView() => InitializeComponent();

    /// <summary>
    /// Ngoại lệ có chủ đích của MVVM: PasswordBox.Password không phải DependencyProperty nên không binding được.
    /// Code-behind chỉ CHÉP giá trị sang ViewModel; việc đăng nhập vẫn do ViewModel làm.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordInput.Password;
        }
    }
}
