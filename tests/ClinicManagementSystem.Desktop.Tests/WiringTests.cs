using System.Reflection;
using System.Text.RegularExpressions;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.DataAccess.DependencyInjection;
using ClinicManagementSystem.Desktop.ViewModels.DependencyInjection;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Desktop.ViewModels.Shell;
using ClinicManagementSystem.Services.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicManagementSystem.Desktop.Tests;

/// <summary>
/// Kiểm tra "dây nối" giữa các phần mà trình biên dịch KHÔNG bắt được:
///  - Đăng ký DI có đủ và đúng vòng đời không (giống lúc App.xaml.cs khởi động).
///  - XAML có bind vào thuộc tính/lệnh không tồn tại không (gõ sai tên -> WPF im lặng hiển thị trống!).
///  - XAML có dùng StaticResource chưa khai báo không (-> app sập lúc mở màn hình).
/// Nhờ vậy dù máy Mac không mở được cửa sổ WPF, ta vẫn bắt được phần lớn lỗi nối dây.
/// </summary>
public sealed class WiringTests
{
    private static readonly string DesktopDir = FindDesktopProject();

    [Fact]
    public void DI_dang_ky_du_va_dung_vong_doi_cho_moi_trang()
    {
        var services = new ServiceCollection();
        services.AddClinicDataAccess("Server=127.0.0.1,1;Database=Khong_ket_noi;User Id=x;Password=x;TrustServerCertificate=True");
        services.AddClinicBusinessServices();
        services.AddClinicViewModels();
        services.AddSingleton<IDialogService>(new FakeDialogs());

        // ValidateOnBuild + ValidateScopes: giống hệt cấu hình trong App.xaml.cs
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var pageTypes = typeof(PageViewModel).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(PageViewModel)) && !t.IsAbstract).ToList();
        Assert.True(pageTypes.Count >= 13);
        foreach (var type in pageTypes)
        {
            Assert.NotNull(provider.GetRequiredService(type));
        }

        Assert.Same(provider.GetRequiredService<ShellViewModel>(), provider.GetRequiredService<ShellViewModel>());
        Assert.NotSame(provider.GetRequiredService<LoginViewModel>(), provider.GetRequiredService<LoginViewModel>());
        // Tầng Services đọc người dùng qua ICurrentUserContext -> phải là CHÍNH phiên đăng nhập.
        Assert.Same(provider.GetRequiredService<UserSession>(),
            provider.GetRequiredService<Common.Security.ICurrentUserContext>());
    }

    [Fact]
    public void Moi_trang_deu_co_View_tuong_ung_trong_ViewMappings()
    {
        var mappings = File.ReadAllText(Path.Combine(DesktopDir, "Themes", "ViewMappings.xaml"));
        var pageTypes = typeof(PageViewModel).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(PageViewModel)) && !t.IsAbstract);
        foreach (var type in pageTypes)
        {
            Assert.Contains($":{type.Name}}}", mappings);
        }
    }

    [Fact]
    public void Binding_trong_XAML_chi_tro_toi_thuoc_tinh_co_that()
    {
        var known = typeof(PageViewModel).Assembly.GetTypes()
            .Concat(typeof(PatientListItemDto).Assembly.GetTypes())
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => p.Name)
            .ToHashSet();

        var problems = new List<string>();
        var checkedCount = 0;
        foreach (var file in Directory.EnumerateFiles(DesktopDir, "*.xaml", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\{Binding\s+(?:Path=)?([A-Za-z_][\w\.]*)([^}]*)"))
            {
                var path = match.Groups[1].Value;
                var rest = match.Groups[2].Value;
                if (path.StartsWith("ElementName") || path.StartsWith("RelativeSource") || path.StartsWith("AdornedElement")
                    || (rest.Contains("ElementName=Root")))
                {
                    continue; // binding vào chính control (PageHeader.Actions, ô lỗi) - không thuộc ViewModel
                }

                checkedCount++;
                var segments = path.Split('.').Where(s => s != "DataContext");
                foreach (var segment in segments.Where(s => !known.Contains(s)))
                {
                    problems.Add($"{Path.GetFileName(file)}: {path} (không có thuộc tính '{segment}')");
                }
            }
        }

        Assert.True(checkedCount > 150, $"Chỉ quét được {checkedCount} binding - biểu thức tìm kiếm có vấn đề.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void StaticResource_trong_XAML_deu_da_khai_bao()
    {
        var files = Directory.EnumerateFiles(DesktopDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        var declared = files.SelectMany(f => Regex.Matches(File.ReadAllText(f), "x:Key=\"(\\w+)\"").Select(m => m.Groups[1].Value)).ToHashSet();

        var missing = files.SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{StaticResource\s+([A-Za-z]\w*)\}")
                .Select(m => $"{Path.GetFileName(f)}: {m.Groups[1].Value}"))
            .Where(x => !declared.Contains(x.Split(": ")[1]))
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    private static string FindDesktopProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ClinicManagementSystem.Desktop");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Không tìm thấy src/ClinicManagementSystem.Desktop");
    }
}
