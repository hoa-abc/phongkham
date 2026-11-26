using System.Collections.ObjectModel;
using ClinicManagementSystem.Common.DTOs;
using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Desktop.ViewModels.Infrastructure;
using ClinicManagementSystem.Services.Contracts;

namespace ClinicManagementSystem.Desktop.ViewModels.Admin;

public sealed class UserRow(UserListItemDto dto)
{
    public string UserName => dto.UserName;
    public string DisplayName => dto.DisplayName;
    public string RoleLabel => dto.Role.ToLabel();
    public string Specialty => dto.Specialty ?? "—";
    public string StatusLabel => dto.IsActive ? "Đang hoạt động" : "Đã khóa";
    public string CreatedAt => Display.Date(dto.CreatedAtUtc);
}

/// <summary>Danh sách tài khoản (chỉ admin). Bản desktop chỉ xem; tạo/sửa tài khoản làm trên bản web.</summary>
public sealed class UserListViewModel(IServiceRunner runner) : PageViewModel
{
    public override string Title => "Người dùng";
    public override string? Subtitle => "Tài khoản nhân viên và vai trò.";

    public ObservableCollection<UserRow> Items { get; } = [];

    protected override async Task OnLoadAsync()
    {
        var items = await runner.RunAsync((IUserService s) => s.ListAsync());
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new UserRow(item));
        }
    }
}
