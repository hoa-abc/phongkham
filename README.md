# Phòng khám Nhi — ứng dụng desktop WPF theo MVVM

Gói này chứa ứng dụng desktop WPF, các project nghiệp vụ/dữ liệu mà ứng dụng sử dụng và test. Ứng dụng hiện kết nối trực tiếp tới SQL Server; Docker Compose trong repo dùng để chạy database phục vụ học tập và demo trên máy cá nhân.

## Cấu trúc project

| Thư mục | Vai trò | Nội dung |
|---|---|---|
| `src/ClinicManagementSystem.Desktop/` | View | Màn hình WPF, style, converter và điểm khởi động ứng dụng (`App.xaml.cs`) |
| `src/ClinicManagementSystem.Desktop.ViewModels/` | ViewModel | Trạng thái màn hình và các lệnh; phần tiếp tân ở `Reception/` |
| `src/ClinicManagementSystem.Services/` | Nghiệp vụ | Đặt lịch, check-in, khám, thu tiền, cấp thuốc |
| `src/ClinicManagementSystem.DataAccess/` | Dữ liệu | Entity, `DbContext`, migration và khởi tạo dữ liệu demo |
| `src/ClinicManagementSystem.Common/` | Dùng chung | DTO, kết quả dịch vụ, enum và tiện ích |
| `tests/ClinicManagementSystem.Desktop.Tests/` | Kiểm thử | ViewModel, wiring/DI và một số kiểm thử tích hợp SQL Server |
| `docs/` | Tài liệu | Bài giảng MVVM desktop |

Mở `ClinicManagementSystem.Mvvm.sln` bằng Visual Studio hoặc mở thư mục repo bằng VS Code. Có thể đọc code theo thứ tự `App.xaml.cs` → `Shell/ShellViewModels.cs` → `Views/Reception/BookingView.xaml` và `Reception/BookingViewModel.cs`.

## Chạy ứng dụng trên Windows

### Yêu cầu

- Windows và .NET 10 SDK.
- Docker Desktop đã khởi động, trạng thái Engine là **Running**.
- Docker CLI và Docker Compose plugin dùng được trong PowerShell (`docker --version`, `docker compose version`). Nếu vừa cài Docker Desktop, hãy mở terminal mới.

### 1. Cấu hình SQL Server

Tại thư mục gốc repo, tạo file `.env` với một dòng sau. Thay giá trị ví dụ bằng mật khẩu riêng, đủ mạnh; không dùng mật khẩu mẫu trong môi trường thật:

```dotenv
MSSQL_SA_PASSWORD=ChangeThis!Sql2026
```

Mật khẩu phải đáp ứng chính sách SQL Server (tối thiểu 8 ký tự và nên có chữ hoa, chữ thường, số, ký tự đặc biệt). Không thêm dấu ngoặc kép kiểu JSON hay chuỗi kết nối vào `.env`.

Khởi động database:

```powershell
docker compose up -d
docker compose ps
```

Chờ container `clinic-web-sqlserver` ở trạng thái `Up`, rồi xác nhận cổng local đã mở:

```powershell
Test-NetConnection 127.0.0.1 -Port 14333
```

`TcpTestSucceeded` cần là `True` trước khi chạy ứng dụng. Compose bind cổng vào `127.0.0.1:14333`, chỉ cho phép kết nối từ chính máy đó.

### 2. Cấu hình ứng dụng

Tạo `src/ClinicManagementSystem.Desktop/appsettings.Local.json` với nội dung dưới đây. Đặt `Password` bằng đúng mật khẩu `MSSQL_SA_PASSWORD` ở bước trước; thay `DemoPassword` bằng mật khẩu demo riêng (ít nhất 8 ký tự):

```json
{
  "ConnectionStrings": {
    "ClinicDb": "Server=127.0.0.1,14333;Database=ClinicWeb;User Id=sa;Password=ChangeThis!Sql2026;TrustServerCertificate=True;Encrypt=True"
  },
  "Seed": {
    "Enabled": true,
    "DemoPassword": "ChangeThis!Demo2026"
  }
}
```

Hai file `.env` và `appsettings.Local.json` chứa thông tin đăng nhập, đã được loại khỏi Git qua `.gitignore`; không commit, gửi kèm bản ZIP công khai hoặc đưa vào hệ thống quản lý mã nguồn. `appsettings.Local.json` được đọc từ thư mục chạy ứng dụng và được copy vào output khi build.

### 3. Chạy app

Tại thư mục gốc repo:

```powershell
dotnet run --project src/ClinicManagementSystem.Desktop
```

Khi `Seed:Enabled` là `true`, lần khởi động đầu sẽ áp dụng migration và tạo dữ liệu/tài khoản demo. Các tài khoản demo gồm `admin`, `letan`, `bacsi`, `bacsi2`, `bacsi3` và `duocsi`; dùng mật khẩu đã đặt ở `Seed:DemoPassword`.

## Dừng database và dữ liệu

```powershell
docker compose stop       # dừng container, giữ dữ liệu
docker compose start      # chạy lại container đã dừng
docker compose down       # gỡ container/network, giữ volume database
```

`docker compose down -v` sẽ xóa volume database và toàn bộ dữ liệu trong đó. Chỉ chạy lệnh này khi chắc chắn muốn làm mới database; hãy sao lưu trước nếu dữ liệu cần giữ.

## Kiểm tra và xử lý lỗi thường gặp

- **`docker` không được nhận diện:** Docker Desktop có thể chưa cài, chưa khởi động hoặc terminal đang mở từ trước khi cài. Mở Docker Desktop, chờ Engine chạy rồi mở PowerShell mới và thử `docker --version`.
- **Không kết nối được SQL Server / transient failure:** chạy `docker compose ps -a`, kiểm tra cổng bằng `Test-NetConnection 127.0.0.1 -Port 14333`, xem log bằng `docker compose logs --tail 100 clinic-db`; bảo đảm Docker đang chạy và app kết nối đúng `127.0.0.1,14333`.
- **Container dừng ngay sau khi khởi động:** xem log trên; kiểm tra mật khẩu trong `.env` đáp ứng chính sách SQL Server. Nếu đổi mật khẩu, cập nhật cùng giá trị trong `appsettings.Local.json`, rồi chạy `docker compose up -d` để áp dụng cấu hình mới.
- **`Failed to load configuration` hoặc lỗi JSON:** kiểm tra dấu ngoặc/dấu phẩy và đảm bảo `DemoPassword` là một chuỗi riêng, không dán chuỗi kết nối vào đó. JSON phải có dạng như ví dụ ở trên.
- **Ứng dụng mở nhưng không có database:** xác nhận `Seed:Enabled` là `true`; phần khởi tạo migration/seed chỉ chạy khi tùy chọn này bật.

## Test và build

Test project (cần .NET 10 SDK):

```powershell
dotnet test tests/ClinicManagementSystem.Desktop.Tests
```

Publish bản Windows x64 tự chứa .NET runtime:

```powershell
dotnet publish src/ClinicManagementSystem.Desktop -c Release -r win-x64 --self-contained true
```

Output publish nằm dưới `src/ClinicManagementSystem.Desktop/bin/Release/`. Bản publish **không đóng gói SQL Server**; máy chạy app vẫn phải truy cập được một SQL Server và có cấu hình kết nối phù hợp. Không phát hành `.env` hoặc `appsettings.Local.json` cùng ứng dụng.

## Lưu ý trước khi deploy thật

Compose hiện tại được thiết kế cho học tập/demo local, không phải cấu hình production. Không mở SQL Server trực tiếp ra Internet và không phân phối thông tin đăng nhập `sa` cho các máy người dùng. Với triển khai nhiều máy hoặc qua Internet, nên đặt một API backend giữa WPF và SQL Server, giới hạn quyền database, quản lý secrets ngoài gói cài đặt, tắt seed tài khoản demo và áp dụng migration trong quy trình triển khai có kiểm soát.

Trên macOS/Linux có thể build/test phần phù hợp, nhưng ứng dụng WPF chỉ chạy trên Windows.
