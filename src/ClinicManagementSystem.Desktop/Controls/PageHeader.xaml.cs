using System.Windows;
using System.Windows.Controls;

namespace ClinicManagementSystem.Desktop.Controls;

public partial class PageHeader : UserControl
{
    /// <summary>Vùng đặt nút thao tác bên phải tiêu đề (ví dụ "＋ Đặt lịch mới").</summary>
    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(PageHeader));

    public PageHeader() => InitializeComponent();

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
