using System.Windows;
using ClinicManagementSystem.Desktop.ViewModels.Shell;

namespace ClinicManagementSystem.Desktop;

/// <summary>
/// Code-behind của cửa sổ chính CHỈ gắn ViewModel. Không có nghiệp vụ nào ở đây -
/// đó chính là tinh thần MVVM: View "mỏng", mọi logic nằm trong ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
