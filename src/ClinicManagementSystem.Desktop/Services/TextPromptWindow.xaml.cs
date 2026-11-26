using System.Windows;
using System.Windows.Controls;

namespace ClinicManagementSystem.Desktop.Services;

/// <summary>Code-behind chỉ lo chuyện hiển thị của hộp thoại; không có nghiệp vụ nào ở đây.</summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
    }

    public string Answer => AnswerBox.Text.Trim();

    private void OnTextChanged(object sender, TextChangedEventArgs e)
        => OkButton.IsEnabled = !string.IsNullOrWhiteSpace(AnswerBox.Text);

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
