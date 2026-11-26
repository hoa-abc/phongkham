using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClinicManagementSystem.Desktop;

/// <summary>
/// Converter = "bộ chuyển đổi" của tầng View: biến giá trị của ViewModel thành thứ WPF cần.
/// Ví dụ ViewModel có bool HasError, còn WPF cần Visibility (Visible/Collapsed).
/// Đặt ở View để ViewModel không phải biết Visibility là gì.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>true = đảo ngược (true -> ẩn).</summary>
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true -> false và ngược lại (ví dụ tích "Mọi ngày" thì khóa ô chọn ngày).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Ẩn phần tử khi giá trị null hoặc chuỗi rỗng.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
