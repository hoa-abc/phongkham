# Bài giảng app desktop MVVM — A · B · C

<!-- page-title: Bài giảng MVVM Desktop -->
<!-- eyebrow: Đồ án Phòng khám Nhi · WPF · MVVM -->
<!-- subtitle: Cùng hệ thống phòng khám, giờ là app desktop WPF theo MVVM. Học phần C thật kỹ, A và B đủ để vấn đáp. -->
<!-- roadmap
Nền tảng MVVM | 40 phút | Mục 0 và 1: bức tranh 5 tầng, binding, command, ObservableCollection, DataTemplate. | 0.
Phần C: C1 → C5 | 60 phút | Đặt lịch, lưới khung giờ, danh sách lịch, lệnh trong từng dòng. | C1.
Phần C: C6 → C11 | 60 phút | Check-in có kiểm tra dữ liệu, tạo hồ sơ, test ViewModel, tự kiểm tra. | C6.
Phần A: khung ứng dụng | 45 phút | App.xaml.cs, DI, IServiceRunner, điều hướng, đăng nhập. | A1.
B, ghép và ôn vấn đáp | 45 phút | Dùng lại tầng dữ liệu, theo dấu một cú bấm, câu hỏi MVVM. | B1.
-->

> Tài liệu này đi kèm app desktop vừa dựng trong dự án (thư mục `src/ClinicManagementSystem.Desktop*`). Cấu trúc giống hệt bài giảng bản web: **Phần C (của bạn) giảng sâu nhất**, A và B đủ để vấn đáp. Khi đã hiểu bản web, bạn sẽ thấy app desktop **dùng lại nguyên** tầng nghiệp vụ và dữ liệu, chỉ thay tầng giao diện. Đó chính là điều thầy muốn bạn hiểu về kiến trúc phân tầng.

---

## 0. Đọc trước: app desktop này là gì, chạy ở đâu

### 0.1. Vì sao có thêm bản desktop

Tài liệu gốc của môn (`Demo_UI_UX/README.md`) yêu cầu: **C# WPF** + **MVVM 5 tầng** + **CommunityToolkit.Mvvm** + **EF Core + SQL Server**. Bản web dùng MVC; bản desktop này làm **đúng yêu cầu MVVM** đó, trên **cùng một database và cùng tầng Services** với bản web.

| Điều cần biết | Chi tiết |
|---|---|
| Công nghệ | .NET 10, WPF, CommunityToolkit.Mvvm 8.4, Microsoft.Extensions.DependencyInjection |
| Chạy ở đâu | **Chỉ Windows** (WPF là công nghệ của Windows). Máy Mac **build** và **chạy test** được, nhưng không mở được cửa sổ |
| Đã kiểm chứng gì | Build không lỗi; 21 test chạy trên Mac, gồm toàn luồng đặt lịch → check-in → khám → thu tiền → cấp thuốc qua ViewModel với SQL Server thật |
| Chưa kiểm chứng | **Giao diện chưa được mở thử trên Windows.** Bố cục, khoảng cách, cỡ chữ có thể cần chỉnh nhẹ khi chạy lần đầu |
| Phạm vi | Đủ 4 vai trò. Phần C (tiếp tân) làm kỹ nhất; admin chỉ xem danh sách tài khoản |

### 0.2. Năm tầng của MVVM trong dự án

```text
┌────────────────────────────────────────────────────────────────────────────┐
│ 1. VIEW          src/ClinicManagementSystem.Desktop          (WPF, XAML)   │
│    BookingView.xaml, MainWindow.xaml ...   chỉ chạy trên Windows           │
└───────────────▲────────────────────────────────────────────────────────────┘
                │ data binding + command (View không gọi Service)
┌───────────────┴────────────────────────────────────────────────────────────┐
│ 2. VIEWMODEL     src/ClinicManagementSystem.Desktop.ViewModels  (C# thuần) │
│    BookingViewModel, ShellViewModel ...    chạy & test được trên Mac       │
└───────────────┬────────────────────────────────────────────────────────────┘
                │ gọi interface qua IServiceRunner
┌───────────────▼────────────────────────────────────────────────────────────┐
│ 3. SERVICE       src/ClinicManagementSystem.Services     (DÙNG LẠI từ web) │
├────────────────────────────────────────────────────────────────────────────┤
│ 4. DATA ACCESS   src/ClinicManagementSystem.DataAccess   (DÙNG LẠI từ web) │
├────────────────────────────────────────────────────────────────────────────┤
│ 5. SQL SERVER    Docker, cổng 14333, database ClinicWeb  (DÙNG CHUNG web)  │
└────────────────────────────────────────────────────────────────────────────┘
        Common (DTO, enum, ServiceResult, ClinicClock): mọi tầng dùng chung
```

Quy tắc vàng: **mũi tên chỉ đi xuống**. View biết ViewModel (qua binding), ViewModel biết Service (qua interface), không tầng nào biết tầng trên.

### 0.3. Ánh xạ bảng phân công sang app desktop

| Thành viên | Bản web | Bản desktop MVVM |
|---|---|---|
| **A** – nền tảng | `Program.cs`, project Web, DI, README | `App.xaml.cs`, `MainWindow`, `ShellViewModel`, `Navigator`, `IServiceRunner`, đăng nhập, `Themes/` |
| **B** – dữ liệu | Entity, `ClinicDbContext`, Fluent API, migration, seed | **Không đổi gì.** Dùng lại nguyên, cùng database |
| **C** – tiếp tân | `Views/Reception`, ViewModel web, CSS | `ViewModels/Reception/*` + `Views/Reception/*.xaml` |

### 0.4. Danh sách project sau khi thêm bản desktop

| Project | Loại | Mới hay dùng lại |
|---|---|---|
| `ClinicManagementSystem.Common` | thư viện | dùng lại |
| `ClinicManagementSystem.DataAccess` | thư viện | dùng lại |
| `ClinicManagementSystem.Services` | thư viện | dùng lại |
| `ClinicManagementSystem.Web` | ASP.NET Core MVC | bản web, giữ nguyên |
| `ClinicManagementSystem.Desktop.ViewModels` | thư viện `net10.0` | **mới** – tầng ViewModel |
| `ClinicManagementSystem.Desktop` | WPF `net10.0-windows` | **mới** – tầng View |
| `ClinicManagementSystem.Desktop.Tests` | xUnit | **mới** – 21 test |

---

## 1. MVVM từ số 0

### 1.1. Ba chữ cái

- **Model**: dữ liệu và nghiệp vụ. Ở đây là **DTO** trong `Common` cùng các **Service** (`IAppointmentService`...). ViewModel không tự viết SQL.
- **View**: thứ người dùng nhìn thấy, viết bằng **XAML** (giống HTML nhưng cho WPF). View **không chứa logic**.
- **ViewModel**: "bộ não" của một màn hình. Nó giữ **trạng thái** (bé nào đang chọn, khung giờ nào, đang tải hay không) và **hành động** (Command). Nó **không biết** View trông ra sao.

Hai thứ nối View với ViewModel: **data binding** (nối dữ liệu) và **command** (nối hành động).

### 1.2. Data binding: "dây nối" dữ liệu

Trong XAML:

```xaml
<TextBox Text="{Binding PatientKeyword, UpdateSourceTrigger=PropertyChanged}" />
```

Nghĩa là: ô nhập này **nối hai chiều** với thuộc tính `PatientKeyword` của ViewModel. Người dùng gõ → ViewModel đổi; ViewModel đổi → ô nhập đổi.

Muốn chiều "ViewModel đổi → màn hình đổi" hoạt động, ViewModel phải **báo** mỗi khi thuộc tính đổi, qua interface `INotifyPropertyChanged`. Viết tay sẽ rất dài:

```csharp
private string? patientKeyword;
public string? PatientKeyword
{
    get => patientKeyword;
    set
    {
        if (patientKeyword == value) return;
        patientKeyword = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PatientKeyword)));  // "báo" cho WPF
    }
}
```

**CommunityToolkit.Mvvm** viết hộ toàn bộ đoạn trên. Bạn chỉ cần:

```csharp
[ObservableProperty]
private string? patientKeyword;   // bộ sinh mã tự tạo thuộc tính PatientKeyword có báo thay đổi
```

Lớp phải là `partial` để bộ sinh mã (source generator) viết thêm phần còn lại vào một file ẩn lúc biên dịch.

### 1.3. Command: "dây nối" hành động

Nút bấm không gọi hàm trực tiếp. Nó gọi một **Command** của ViewModel:

```xaml
<Button Content="Xác nhận lịch" Command="{Binding ConfirmCommand}" />
```

```csharp
[RelayCommand(CanExecute = nameof(CanConfirm))]
private Task ConfirmAsync() { ... }          // bộ sinh mã tạo thuộc tính ConfirmCommand

private bool CanConfirm() => SelectedPatient is not null && SelectedSlot is { IsAvailable: true };
```

- Hàm tên `ConfirmAsync` → lệnh tên `ConfirmCommand` (bỏ chữ Async, thêm Command).
- **`CanExecute`**: khi trả `false`, WPF **tự làm mờ nút**. Không cần viết `button.IsEnabled = false` ở đâu cả.
- Lệnh async còn tự chặn bấm hai lần trong lúc đang chạy.

### 1.4. ObservableCollection: danh sách tự cập nhật

`List<T>` thường không báo khi thêm hoặc xóa phần tử. `ObservableCollection<T>` thì có: `Slots.Add(...)` là lưới giờ trên màn hình tự hiện thêm ô; `Lines.Remove(line)` là bảng thuốc tự mất một dòng.

### 1.5. DataContext và DataTemplate

- **DataContext** = "ViewModel mà View này đang hiển thị". Mọi `{Binding X}` tìm `X` trong DataContext.
- **DataTemplate** = "dữ liệu kiểu này thì vẽ như thế này". Ví dụ ở [ViewMappings.xaml](../src/ClinicManagementSystem.Desktop/Themes/ViewMappings.xaml):

```xaml
<DataTemplate DataType="{x:Type rec:BookingViewModel}"><vrec:BookingView /></DataTemplate>
```

Khi vùng nội dung nhận một `BookingViewModel`, WPF tự vẽ `BookingView` và gán DataContext = ViewModel đó.

### 1.6. MVC (bản web) và MVVM (bản desktop): cùng một màn hình đặt lịch

| | Bản web (MVC) | Bản desktop (MVVM) |
|---|---|---|
| Ai nhận cú bấm | `AppointmentsController.Create` (POST) | `BookingViewModel.ConfirmCommand` |
| Đổi bác sĩ thì sao | Form GET gửi lại, server dựng **cả trang mới** | `OnSelectedDoctorChanged` tự chạy, **chỉ lưới giờ** được tải lại |
| Trạng thái giữa hai lần thao tác | Mất sau mỗi request, phải mang theo bằng ô ẩn, URL | ViewModel **sống** suốt lúc màn hình mở |
| Nút bị mờ khi chưa đủ điều kiện | Không có, phải gửi form mới biết | Có, nhờ `CanExecute` |
| Kiểm tra ô nhập | Khi gửi form (`ModelState`) | Ngay khi gõ (`ObservableValidator`) |
| Bảng thuốc thêm/xóa dòng | Cần JavaScript | `ObservableCollection`, không cần gì thêm |
| Chống CSRF, cookie | Cần | Không cần, vì không có trình duyệt |
| Điểm chung | **Tách giao diện khỏi nghiệp vụ.** Cả hai gọi đúng cùng `IAppointmentService` | |

---

# PHẦN C — LUỒNG TIẾP TÂN BẰNG MVVM

## C1. Bản đồ file của phần C

| Màn hình | ViewModel (C# thuần) | View (XAML) | Service gọi tới |
|---|---|---|---|
| Danh sách lịch hẹn | [AppointmentListViewModel.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/AppointmentListViewModel.cs) | [AppointmentListView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/AppointmentListView.xaml) | `IAppointmentService` |
| **Đặt lịch** ⭐ | [BookingViewModel.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs) | [BookingView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml) | `IUserService`, `IPatientService`, `IAppointmentService` |
| Hàng chờ check-in | [CheckInViewModels.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/CheckInViewModels.cs) | [CheckInQueueView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/CheckInQueueView.xaml) | `IReceptionService` |
| Ghi sinh hiệu | (cùng file trên) | [CheckInView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/CheckInView.xaml) | `IReceptionService` |
| Hồ sơ bệnh nhi, tạo hồ sơ | [PatientViewModels.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/PatientViewModels.cs) | [PatientListView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/PatientListView.xaml), [CreatePatientView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/CreatePatientView.xaml) | `IPatientService` |
| Dòng hiển thị | [ReceptionRows.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/ReceptionRows.cs) | — | — |

Đúng quy ước của nhóm: phần C nằm trong `ViewModels/Reception` và `Views/Reception`.

## C2. BookingViewModel: đọc từng khối

📁 [BookingViewModel.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs)

### Khối 1: khai báo lớp

```csharp
public sealed partial class BookingViewModel(IServiceRunner runner, INavigator navigator) : PageViewModel
```

- `partial`: bắt buộc để CommunityToolkit sinh thêm code.
- `(IServiceRunner runner, INavigator navigator)`: **primary constructor**. DI đưa vào "cách gọi Service" và "cách chuyển trang". ViewModel không tự `new` gì cả.
- `: PageViewModel`: được sẵn `Title`, `LoadCommand`, `IsBusy`, `ErrorMessage`, `SuccessMessage` (xem C8).

### Khối 2: trạng thái của màn hình (bước 1 và bước 2)

[BookingViewModel.cs:38](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs:38)

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasPatient))]        // SelectedPatient đổi -> báo luôn HasPatient đổi
[NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]  // ... và bảo nút Xác nhận kiểm tra lại CanExecute
private PatientRow? selectedPatient;

public ObservableCollection<SlotItem> Slots { get; } = [];   // lưới khung giờ
```

> ❓ **"`NotifyCanExecuteChangedFor` để làm gì?"** — Nút "Xác nhận lịch" chỉ hỏi lại `CanConfirm()` khi được báo. Mỗi khi bé, bác sĩ hoặc khung giờ đổi, thuộc tính này báo cho nút kiểm tra lại, nên nút tự sáng lên hoặc mờ đi đúng lúc.

### Khối 3: nạp dữ liệu khi mở trang

[BookingViewModel.cs:71](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs:71)

```csharp
protected override async Task OnLoadAsync()
{
    var doctors = await runner.RunAsync((IUserService s) => s.GetDoctorsAsync());
    ...
    await LoadPatientsAsync();
    if (PreselectedPatientId is { } id)
        SelectedPatient = Patients.FirstOrDefault(x => x.PatientId == id);   // mở từ hồ sơ bé -> chọn sẵn
    SelectedDoctor ??= Doctors.FirstOrDefault();  // gán bác sĩ -> kích hoạt tải lưới giờ (khối 4)
}
```

`runner.RunAsync((IUserService s) => ...)` nghĩa là: "lấy cho tôi một `IUserService` rồi chạy việc này". Vì sao phải đi qua `runner` thay vì nhận `IUserService` trực tiếp? Xem **A4**, câu hỏi hay bị hỏi nhất của bản desktop.

### Khối 4: đổi bác sĩ hoặc ngày thì lưới giờ tự tải lại

[BookingViewModel.cs:107](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs:107)

```csharp
partial void OnSelectedDoctorChanged(DoctorOptionDto? value) => LoadSlotsCommand.Execute(null);
```

Với mỗi `[ObservableProperty]`, bộ sinh mã tạo sẵn một "móc" `partial void On<TênThuộcTính>Changed`. Ta viết thân hàm, và nó **tự chạy mỗi khi giá trị đổi**. Nhờ vậy XAML không cần sự kiện `SelectionChanged`, code-behind trống trơn.

> ❓ **"Bản web cần hai form, sao bản desktop không cần?"** — Web phải gửi request lên server để có lưới giờ mới, nên cần form GET. Ở desktop, ViewModel nằm ngay trong bộ nhớ: đổi bác sĩ là chạy hàm tải lưới luôn.

### Khối 5: tải lưới giờ an toàn khi bấm liên tục

[BookingViewModel.cs:118](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs:118)

```csharp
private int slotRequestVersion;

private async Task ReloadSlotsAsync()
{
    var version = ++slotRequestVersion;          // đánh số lần tải này
    ...
    var slots = await runner.RunAsync((IAppointmentService s) => s.GetSlotsAsync(SelectedDoctor.UserId, date));
    if (version != slotRequestVersion) return;   // đã có lần tải MỚI hơn -> bỏ kết quả cũ
    Slots.Clear();
    foreach (var slot in slots) Slots.Add(new SlotItem(slot));
}
```

Tình huống thật: tiếp tân đổi bác sĩ A sang bác sĩ B rất nhanh. Hai lần tải chạy chồng nhau, lần của A có thể về **sau** lần của B. Nếu không đánh số, lưới sẽ hiện khung giờ của bác sĩ A dù ô chọn đang là B. Đây là một **race condition** (tranh chấp thứ tự) ở phía giao diện.

### Khối 6: xác nhận

[BookingViewModel.cs:151](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs:151)

```csharp
private bool CanConfirm() => SelectedPatient is not null && SelectedDoctor is not null
                             && SelectedSlot is { IsAvailable: true } && SelectedDate is not null;

[RelayCommand(CanExecute = nameof(CanConfirm))]
private Task ConfirmAsync() => RunBusyAsync(async () =>
{
    var slot = SelectedSlot!; var doctor = SelectedDoctor!; var patient = SelectedPatient!;   // chụp lại lựa chọn
    var startUtc = ClinicClock.ToUtc(date.ToDateTime(slot.Start));                          // giờ VN -> UTC
    var result = await runner.RunAsync((IAppointmentService s) => s.CreateAppointmentAsync(
        new CreateAppointmentDto(patient.PatientId, doctor.UserId, startUtc, startUtc.AddMinutes(30), Reason)));

    if (result.Succeeded) SuccessMessage = $"{result.Message} {patient.FullName} ({patient.MedicalRecordNumber}) ...";
    else ErrorMessage = result.Message;   // ví dụ: "Khung giờ này vừa được đặt..."

    await ReloadSlotsAsync();             // thành công hay thất bại đều tải lại lưới MỚI NHẤT
}, clearSuccess: true);
```

- **Chụp lựa chọn vào biến cục bộ** trước khi `await`: trong lúc chờ database, người dùng có thể bấm chọn thứ khác.
- Service vẫn là **trọng tài cuối cùng**: ba lớp chống trùng (truy vấn chồng lấn + transaction Serializable + unique index) y như bản web. `CanExecute` chỉ giúp người dùng ít sai. → Hướng dẫn gốc mục 5.3.
- Bị trùng thì **giữ** bé, bác sĩ và ngày, chỉ bỏ khung giờ. Đây là quyết định UX giống bản web.

## C3. BookingView.xaml: đọc binding từng chỗ

📁 [BookingView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml)

| Chỗ trên màn hình | XAML | Nối vào ViewModel |
|---|---|---|
| Ô tìm bé | `Text="{Binding PatientKeyword, UpdateSourceTrigger=PropertyChanged}"` + `KeyBinding Key="Enter"` | `PatientKeyword`, `SearchPatientsCommand` |
| Danh sách bé ([dòng 44](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml:44)) | `ItemsSource="{Binding Patients}" SelectedItem="{Binding SelectedPatient}"` | `Patients`, `SelectedPatient` |
| Ô chọn bác sĩ ([dòng 88](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml:88)) | `SelectedItem="{Binding SelectedDoctor}"` | đổi là chạy `OnSelectedDoctorChanged` |
| Ngày khám | `DatePicker SelectedDate="{Binding SelectedDate}" DisplayDateStart="{Binding MinDate}"` | không cho chọn ngày quá khứ |
| Lưới giờ ([dòng 105](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml:105)) | `ListBox ItemsSource="{Binding Slots}"` + `UniformGrid Columns="5"` | 5 khung mỗi hàng |
| Khung bận bị khóa ([dòng 114](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml:114)) | `<Setter Property="IsEnabled" Value="{Binding IsAvailable}" />` | `SlotItem.IsAvailable` |
| Nút xác nhận ([dòng 145](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml:145)) | `Command="{Binding ConfirmCommand}"` | tự mờ nhờ `CanConfirm` |
| Tóm tắt bên phải | `Text="{Binding SelectedPatient.FullName}"` | đường dẫn nhiều cấp: bé đang chọn → tên |

> ❓ **"`ItemsPanel` và `ItemTemplate` khác nhau thế nào?"** — `ItemsPanel` quyết định **cách xếp** các phần tử (ở đây `UniformGrid` 5 cột thay vì xếp dọc). `ItemTemplate` quyết định **mỗi phần tử trông ra sao** (giờ to + chú thích nhỏ).

## C4. Ba loại lớp dữ liệu ở bản desktop

| | DTO (Common) | "Row" (ViewModel) | ViewModel trang |
|---|---|---|---|
| Ví dụ | `AppointmentListItemDto` | `AppointmentRow` | `AppointmentListViewModel` |
| Giữ gì | Dữ liệu thô từ Service (giờ UTC, SĐT dạng số) | **Chữ sẵn sàng hiển thị**: `Time` (giờ VN), `GuardianLine`, `StatusLabel`, `CanAct` | Trạng thái và lệnh của cả trang |
| Báo thay đổi? | Không (bất biến) | Không cần (chỉ đọc) | Có |

📁 [ReceptionRows.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/ReceptionRows.cs). `AppointmentRow` bọc DTO để XAML chỉ việc `{Binding GuardianLine}` thay vì tự ghép chuỗi hay tự đổi giờ. Nhờ vậy test cũng kiểm tra được cách hiển thị.

## C5. Danh sách lịch: lệnh nằm trong từng dòng

📁 [AppointmentListView.xaml:88](../src/ClinicManagementSystem.Desktop/Views/Reception/AppointmentListView.xaml:88)

```xaml
<Button Content="Check-in"
        Command="{Binding DataContext.CheckInCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
        CommandParameter="{Binding}" />
```

Bên trong một dòng của bảng, DataContext là **dòng đó** (`AppointmentRow`), không phải ViewModel của trang. Muốn gọi lệnh của trang, ta "đi ngược lên" tới DataGrid (`RelativeSource AncestorType=DataGrid`) rồi lấy DataContext của nó. `CommandParameter="{Binding}"` gửi **chính dòng đó** làm tham số, nên ViewModel biết bấm vào lịch nào.

**Hủy lịch hỏi lý do** mà ViewModel không biết hộp thoại là gì:

```csharp
var reason = dialogs.AskText("Hủy lịch hẹn", $"Lý do hủy lịch {row.Time} ...");
if (reason is null) return;   // người dùng bấm Hủy trên hộp thoại
```

`dialogs` là `IDialogService`. Khi chạy thật, nó là [WpfDialogService](../src/ClinicManagementSystem.Desktop/Services/WpfDialogService.cs) mở cửa sổ nhỏ. Khi test, nó là bản giả luôn trả "Lý do kiểm thử". → Xem A7.

## C6. Check-in: kiểm tra dữ liệu ngay khi gõ

📁 [CheckInViewModels.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/CheckInViewModels.cs) · [CheckInView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/CheckInView.xaml)

```csharp
[ObservableProperty]
[NotifyDataErrorInfo]                                  // kiểm tra NGAY khi giá trị đổi
[NotifyPropertyChangedFor(nameof(IsFever))]
[Required(ErrorMessage = "Vui lòng nhập nhiệt độ.")]
[Range(30d, 45d, ErrorMessage = "Nhiệt độ phải từ 30 đến 45 °C.")]
private decimal? temperatureC;

public bool IsFever => TemperatureC >= 38;             // hiện cảnh báo vàng ngay khi rời ô
```

- Các thuộc tính `[Required]`, `[Range]` **giống hệt** ViewModel bên web. Đây là kiến thức dùng lại được.
- `ViewModelBase` kế thừa `ObservableValidator`, lớp này cài sẵn `INotifyDataErrorInfo`. WPF đọc interface này và **tự viền đỏ** ô sai. Dòng chữ lỗi bên dưới đến từ `FieldErrorTemplate` trong [Styles.xaml](../src/ClinicManagementSystem.Desktop/Themes/Styles.xaml).
- Khi bấm xác nhận: `ValidateAllProperties()` kiểm tra cả những ô người dùng **chưa chạm vào**. Có lỗi thì dừng, không gọi Service.
- `Text="{Binding WeightKg, TargetNullValue=''}"`: ô trống tương ứng `null`, để `[Required]` bắt được "chưa nhập".
- Service vẫn kiểm tra khoảng giá trị lần nữa. Không bao giờ tin riêng giao diện.

Thành công thì chuyển về hàng chờ kèm thông báo xanh:

```csharp
navigator.NavigateTo<CheckInQueueViewModel>(page => page.SuccessMessage = message);
```

## C7. Hồ sơ bệnh nhi: tạo xong chuyển thẳng sang đặt lịch

📁 [PatientViewModels.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/PatientViewModels.cs)

- `PatientListViewModel.CanManage`: bác sĩ cũng mở được trang này để tra cứu, nhưng **không thấy** nút "Tạo hồ sơ" hay "Đặt lịch". Ẩn nút chỉ để gọn; quyền thật do `PermissionGuard` kiểm tra.
- `CreatePatientViewModel` tra **SĐT phụ huynh** khi gõ đủ số. XAML dùng `Delay=400` để đợi người dùng ngừng gõ 0,4 giây rồi mới tra.
- Lưu xong:

```csharp
navigator.NavigateTo<BookingViewModel>(page =>
{
    page.PreselectedPatientId = patientId;   // mở trang đặt lịch và CHỌN SẴN bé vừa tạo
    page.SuccessMessage = result.Message;
});
```

Luồng tiếp tân không bị đứt: **bé mới đến → tạo hồ sơ → đặt lịch ngay** mà không phải tìm lại.

## C8. Trạng thái bận và thông báo: viết một lần, dùng mọi trang

📁 [ViewModelBase.cs:40](../src/ClinicManagementSystem.Desktop.ViewModels/Infrastructure/ViewModelBase.cs:40) · [MessageBar.xaml](../src/ClinicManagementSystem.Desktop/Controls/MessageBar.xaml)

```csharp
protected async Task RunBusyAsync(Func<Task> work, bool clearSuccess = false)
{
    ErrorMessage = null; ...
    IsBusy = true;
    try { await work(); }
    catch (UnauthorizedAccessException e) { ErrorMessage = e.Message; }      // sai quyền
    catch (Exception e) { ErrorMessage = "Không thực hiện được thao tác: " + e.Message; }  // mất kết nối DB...
    finally { IsBusy = false; }
}
```

Mọi thao tác đều đi qua hàm này, nên: (1) đang chạy thì hiện thanh tiến trình; (2) **app không bao giờ sập** vì lỗi nghiệp vụ hay mất kết nối; (3) lỗi hiện thành dòng chữ đỏ dễ hiểu. `MessageBar` là một UserControl đặt ở mọi trang, tự bind vào `IsBusy`, `ErrorMessage`, `SuccessMessage` của trang chứa nó.

## C9. Test ViewModel: lợi ích lớn nhất của MVVM

📁 [ViewModelTests.cs](../tests/ClinicManagementSystem.Desktop.Tests/ViewModelTests.cs) · [Fakes.cs](../tests/ClinicManagementSystem.Desktop.Tests/Fakes.cs)

Vì ViewModel là C# thuần và chỉ biết interface, ta test nó bằng **service giả**, không cần cửa sổ, không cần database:

```csharp
[Fact]
public async Task Khung_gio_vua_bi_quay_khac_dat_thi_bao_loi_giu_lua_chon_va_bo_khung_cu()
{
    appointments.NextResult = ServiceResult<AppointmentDto>.Failure("APPOINTMENT_SLOT_TAKEN", "Khung giờ này vừa được đặt...");
    var vm = CreateViewModel();
    await vm.LoadCommand.ExecuteAsync(null);
    vm.SelectedPatient = vm.Patients[0];
    vm.SelectedSlot = vm.Slots[2];

    await vm.ConfirmCommand.ExecuteAsync(null);      // "bấm nút" bằng code

    Assert.Equal("Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.", vm.ErrorMessage);
    Assert.NotNull(vm.SelectedPatient);   // không bắt chọn lại bé
    Assert.Null(vm.SelectedSlot);         // khung cũ đã mất
}
```

Các test của phần C kiểm tra: tự tải lưới khi đổi bác sĩ/ngày; nút xác nhận chỉ sáng khi đủ điều kiện; gửi đúng giờ UTC (08:00 giờ Việt Nam là 01:00 UTC); bị trùng thì giữ lựa chọn; check-in bỏ trống thì báo lỗi và **không** gọi Service; sốt từ 38 °C thì hiện cảnh báo.

> ❓ **"MVC test được Controller, sao lại nói MVVM dễ test hơn?"** — Controller trả về View/Redirect, muốn kiểm "màn hình đang hiện gì" vẫn khó. ViewModel **chính là** trạng thái màn hình: `vm.SelectedSlot`, `vm.ErrorMessage`, `vm.ConfirmCommand.CanExecute(...)`. Kiểm ViewModel gần như kiểm luôn giao diện, chỉ trừ phần vẽ.

## C10. So sánh phần C: web và desktop

| Việc | Web (file của C) | Desktop (file của C) |
|---|---|---|
| Danh sách lịch | `Views/Appointments/Index.cshtml` + Controller | `AppointmentListView.xaml` + `AppointmentListViewModel` |
| Đặt lịch | 2 form + `data-autosubmit` (JavaScript) | Binding + `OnSelectedDoctorChanged` |
| Khóa khung bận | `disabled="@(!slot.IsAvailable)"` | `IsEnabled="{Binding IsAvailable}"` |
| Hủy lịch hỏi lý do | `<details>` + ô nhập trong form | `IDialogService.AskText` |
| Kiểm tra ô nhập | `[Required]` + `ModelState` khi gửi | `[Required]` + `ObservableValidator` khi gõ |
| Thông báo sau khi lưu | `TempData` + PRG | `SuccessMessage` trên ViewModel |
| CSS riêng module | `reception.css` | style trong `Themes/Styles.xaml` |

## C11. Tự kiểm tra phần C

1. Vì sao lớp ViewModel phải có chữ `partial`?
2. `[ObservableProperty] private PatientRow? selectedPatient;` sinh ra những gì?
3. `NotifyCanExecuteChangedFor(nameof(ConfirmCommand))` giải quyết vấn đề gì?
4. Hàm `OnSelectedDoctorChanged` được gọi khi nào, do ai gọi?
5. Vì sao `ReloadSlotsAsync` phải đánh số lần tải?
6. Trong một dòng DataGrid, làm sao nút gọi được lệnh của trang? `CommandParameter="{Binding}"` gửi gì?
7. Vì sao ViewModel không gọi `MessageBox.Show` trực tiếp?
8. `[NotifyDataErrorInfo]` và `ValidateAllProperties()` khác nhau thế nào?
9. `TargetNullValue=''` để làm gì?
10. Kể 3 điều test ViewModel kiểm tra được mà không cần mở cửa sổ.

**Thực hành (đọc code, không cần Windows):**

1. Mở [BookingViewModel.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Reception/BookingViewModel.cs) và [BookingView.xaml](../src/ClinicManagementSystem.Desktop/Views/Reception/BookingView.xaml) cạnh nhau. Với mỗi `{Binding X}` trong XAML, tìm `X` trong ViewModel.
2. Chạy test trong Terminal (chạy được trên Mac): `dotnet test tests/ClinicManagementSystem.Desktop.Tests`.
3. Thử làm hỏng: sửa `CanConfirm` để luôn trả `true` rồi chạy lại test. Xem test nào báo đỏ và vì sao. Nhớ sửa lại.

---

# PHẦN A — NỀN TẢNG VÀ TÍCH HỢP

## A1. Vì sao tách ViewModel thành project riêng

📁 [ClinicManagementSystem.Desktop.ViewModels.csproj](../src/ClinicManagementSystem.Desktop.ViewModels/ClinicManagementSystem.Desktop.ViewModels.csproj) · [ClinicManagementSystem.Desktop.csproj](../src/ClinicManagementSystem.Desktop/ClinicManagementSystem.Desktop.csproj)

| Project | TargetFramework | Có WPF? | Chạy trên Mac? |
|---|---|---|---|
| `Desktop.ViewModels` | `net10.0` | **Không** | Có (build + test) |
| `Desktop` | `net10.0-windows` | Có (`UseWPF`) | Chỉ build (`EnableWindowsTargeting`) |

Tách như vậy **bắt buộc** ViewModel không dùng được bất kỳ thứ gì của WPF: không `MessageBox`, không `Visibility`, không `Button`. Nếu lỡ viết, trình biên dịch báo lỗi ngay. Đây là cách "dùng trình biên dịch để giữ kỷ luật MVVM", đồng thời giúp test ViewModel chạy trên mọi máy.

Chiều tham chiếu:

```text
Desktop (WPF) ──► Desktop.ViewModels ──► Services ──► DataAccess ──► Common
      └──────────────────────────────────► Services / DataAccess (chỉ để đăng ký DI trong App.xaml.cs)
```

## A2. App.xaml.cs: composition root của bản desktop

📁 [App.xaml.cs](../src/ClinicManagementSystem.Desktop/App.xaml.cs). Vai trò giống `Program.cs` bên web.

| Bước | Dòng | Làm gì |
|---|---|---|
| 1 | đầu hàm | Đọc `appsettings.json`, `appsettings.Local.json` (bí mật, không lên Git), biến môi trường |
| 2 | [52-56](../src/ClinicManagementSystem.Desktop/App.xaml.cs:52) | `AddClinicDataAccess` + `AddClinicBusinessServices` (**y như web**) + `AddClinicViewModels` + `WpfDialogService` + `MainWindow` |
| 3 | [60](../src/ClinicManagementSystem.Desktop/App.xaml.cs:60) | `BuildServiceProvider(ValidateScopes, ValidateOnBuild)`: đăng ký DI sai thì báo **ngay khi mở app** |
| 4 | sau đó | Nếu `Seed:Enabled` thì chạy `DbInitializer` (migration + dữ liệu mẫu), lỗi thì hiện hộp thoại dễ hiểu |
| 5 | [85](../src/ClinicManagementSystem.Desktop/App.xaml.cs:85) | Mở `MainWindow`, gọi `ShellViewModel.Start()` → màn hình đăng nhập |

`App.xaml` **không có** `StartupUri`, vì cửa sổ chính cần được DI tạo ra để nhận `ShellViewModel` qua constructor.

## A3. Vòng đời DI trong app desktop

📁 [ViewModelServiceCollectionExtensions.cs](../src/ClinicManagementSystem.Desktop.ViewModels/DependencyInjection/ViewModelServiceCollectionExtensions.cs)

| Đăng ký | Vòng đời | Vì sao |
|---|---|---|
| `UserSession` (+ `ICurrentUserContext`) | Singleton | Một phiên đăng nhập cho cả app |
| `IServiceRunner`, `INavigator`, `ShellViewModel` | Singleton | Sống suốt app |
| Mọi trang (`BookingViewModel`...) | **Transient** | Mỗi lần mở trang là một ViewModel mới, dữ liệu tươi |
| `ClinicDbContext`, các Service | Scoped | Giữ nguyên như web, dùng qua `IServiceRunner` |

> ❓ **"Trên web, Scoped nghĩa là một request. Desktop không có request, vậy Scoped là gì?"** — Scope phải **tự tạo**. Đó chính là việc của `IServiceRunner` (A4).

## A4. IServiceRunner: mỗi thao tác một DbContext ⭐

📁 [AppServices.cs:24](../src/ClinicManagementSystem.Desktop.ViewModels/Infrastructure/AppServices.cs:24)

```csharp
public sealed class ScopedServiceRunner(IServiceScopeFactory scopeFactory) : IServiceRunner
{
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();   // = "một request" của bản web
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await work(service);
    }                                                               // hết khối: scope hủy, DbContext hủy
}
```

**Vì sao không tiêm thẳng `IAppointmentService` vào ViewModel?** Có hai cách làm sai:

| Cách sai | Hậu quả |
|---|---|
| Đăng ký ViewModel là Singleton và tiêm Service vào | Lỗi **captive dependency**: một DbContext dùng chung suốt app. Dữ liệu **cũ** (DbContext nhớ những gì đã đọc, không thấy quầy khác vừa đặt lịch), và hai thao tác chạy cùng lúc trên một DbContext sẽ lỗi (DbContext **không an toàn đa luồng**) |
| Tiêm Service vào ViewModel Transient | Trang mở lâu thì DbContext của trang cũng sống lâu: vẫn dữ liệu cũ, vẫn rủi ro hai thao tác cùng lúc |

Cách đúng: **mỗi lần bấm nút = một scope mới = một DbContext mới**, xong thì hủy. Bản desktop vì thế có cùng đặc tính an toàn như "mỗi request một DbContext" của bản web. Ngoài ra, `ValidateScopes = true` trong `App.xaml.cs` sẽ báo lỗi ngay nếu ai đó lỡ lấy Service Scoped từ gốc.

> ❓ **"Cú pháp `(IUserService s) => s.GetDoctorsAsync()` có gì đặc biệt?"** — Ghi rõ kiểu tham số `IUserService` để C# **tự suy ra** cả hai kiểu generic (`TService` = `IUserService`, `TResult` = danh sách bác sĩ). Không có nó, ta phải viết dài: `RunAsync<IUserService, IReadOnlyList<DoctorOptionDto>>(...)`.

## A5. Cửa sổ chính và điều hướng

📁 [MainWindow.xaml](../src/ClinicManagementSystem.Desktop/MainWindow.xaml) · [ShellViewModels.cs](../src/ClinicManagementSystem.Desktop.ViewModels/Shell/ShellViewModels.cs) · [AppServices.cs:58](../src/ClinicManagementSystem.Desktop.ViewModels/Infrastructure/AppServices.cs:58)

```text
MainWindow (DataContext = ShellViewModel)
├── Menu trái: ListBox ItemsSource=MenuItems, SelectedItem=SelectedMenuItem
│        chọn mục -> OnSelectedMenuItemChanged -> mục.Navigate(navigator)
└── Vùng nội dung: ContentControl Content="{Binding Navigator.CurrentPage}"
         Navigator.NavigateTo<BookingViewModel>() -> CurrentPage = ViewModel mới
         -> WPF tra ViewMappings.xaml -> vẽ BookingView
```

- **Điều hướng kiểu ViewModel-first**: code chỉ nói "mở `BookingViewModel`". View nào vẽ nó là việc của `ViewMappings.xaml`. ViewModel không biết View tồn tại.
- `NavigateTo<T>(configure)` cho phép **truyền tham số** sang trang mới (mã lịch cần check-in, bé cần đặt lịch).
- `ShellViewModel.MenuFor(role)` ([dòng 82](../src/ClinicManagementSystem.Desktop.ViewModels/Shell/ShellViewModels.cs:82)): menu theo vai trò, giống menu bản web. Ẩn menu **không phải** bảo mật.

## A6. Đăng nhập và phiên làm việc

- `UserSession` ([AppServices.cs:77](../src/ClinicManagementSystem.Desktop.ViewModels/Infrastructure/AppServices.cs:77)) thay cho cookie bên web. Nó **đồng thời** là `ICurrentUserContext`, nên tầng Services biết "ai đang thao tác" để kiểm quyền và ghi nhật ký, giống hệt bản web.
- Đăng nhập thành công → `session.SignIn(user)` → sự kiện `Changed` → `ShellViewModel` dựng menu theo vai trò và mở trang đầu tiên. Đăng xuất làm ngược lại.
- **Ngoại lệ có chủ đích của MVVM**: `PasswordBox` của WPF **cố ý không cho binding** mật khẩu, để mật khẩu không nằm lung tung trong bộ nhớ. Code-behind [LoginView.xaml.cs:15](../src/ClinicManagementSystem.Desktop/Views/Shell/LoginView.xaml.cs:15) chỉ chép mật khẩu sang ViewModel; ViewModel xóa nó ngay sau khi đăng nhập.

> ❓ **"MVVM nói code-behind phải trống, vậy LoginView có code-behind là sai?"** — MVVM không cấm code-behind. Nó cấm **logic nghiệp vụ** trong code-behind. Chép giá trị từ một control không bind được là chuyện **hiển thị**. Việc đăng nhập vẫn nằm trong ViewModel.

## A7. Dịch vụ giao diện, style, converter

- **`IDialogService`**: ViewModel chỉ biết interface `Confirm` / `AskText`. WPF có [WpfDialogService](../src/ClinicManagementSystem.Desktop/Services/WpfDialogService.cs) + [TextPromptWindow](../src/ClinicManagementSystem.Desktop/Services/TextPromptWindow.xaml); test có `FakeDialogs`.
- **[Styles.xaml](../src/ClinicManagementSystem.Desktop/Themes/Styles.xaml)**: màu khai báo một lần (`PrimaryBrush` #4F7DFF lấy theo bản web và Figma); style cho nút, ô nhập, bảng, thẻ; mẫu lỗi `FieldErrorTemplate`; mẫu hiển thị "dải nhận diện bệnh nhi" (`DataTemplate DataType=PatientBanner`). Đây là "CSS" của WPF.
- **[Converters.cs](../src/ClinicManagementSystem.Desktop/Converters.cs)**: ViewModel có `bool HasError`, WPF cần `Visibility`. Converter đứng ở tầng View đổi kiểu, để ViewModel không phải biết `Visibility` là gì.

## A8. Cấu hình và chạy trên Windows

1. Cài **.NET 10 SDK** và **Docker Desktop** trên máy Windows; bật SQL Server bằng `docker compose up -d` như bản web.
2. Chép `src/ClinicManagementSystem.Desktop/appsettings.Local.example.json` thành `appsettings.Local.json`, điền mật khẩu `sa` và mật khẩu demo (giống bản web).
3. Chạy: `dotnet run --project src/ClinicManagementSystem.Desktop`, hoặc mở `ClinicManagementSystem.sln` bằng Visual Studio 2022+ và chọn project Desktop làm Startup.
4. Đăng nhập `letan`, `bacsi`, `duocsi`, `admin`.

Bản desktop và bản web dùng **chung database `ClinicWeb`**: đặt lịch trên web thì app desktop thấy ngay, và ngược lại.

---

# PHẦN B — DỮ LIỆU VÀ NGHIỆP VỤ NỀN

## B1. Không sửa một dòng nào ở tầng dữ liệu

App desktop **không thay đổi** `ClinicManagementSystem.DataAccess` (Entity, `ClinicDbContext`, Fluent API, migration, seed) hay `ClinicManagementSystem.Services`. Bằng chứng trong code: [App.xaml.cs:52-53](../src/ClinicManagementSystem.Desktop/App.xaml.cs:52) gọi đúng hai hàm đăng ký mà `Program.cs` bên web gọi.

> ❓ **"Điều này chứng minh gì về kiến trúc?"** — Tầng nghiệp vụ và dữ liệu **không phụ thuộc giao diện**. Đổi từ web sang desktop chỉ phải viết lại tầng trên cùng. Mọi quy tắc quan trọng (chống trùng lịch, phân quyền, cấp thuốc nguyên tử, nhật ký) **tự động có** ở bản desktop mà không phải viết lại hay kiểm thử lại.

Toàn bộ kiến thức B (ERD, quan hệ Guardian–Patient, Fluent API, filtered unique index, RowVersion, migration, seed) giữ nguyên → xem Phần B của [BAI_GIANG_THEO_PHAN_CONG.md](BAI_GIANG_THEO_PHAN_CONG.md).

## B2. Vòng đời DbContext: web và desktop

| | Web | Desktop |
|---|---|---|
| Ai tạo scope | ASP.NET Core, mỗi HTTP request | `ScopedServiceRunner`, mỗi thao tác |
| DbContext sống bao lâu | Một request | Một lần bấm nút |
| Tránh dữ liệu cũ? | Có | Có |
| Tránh hai thao tác cùng một DbContext? | Có | Có |

## B3. Hai giao diện, một database: chống trùng vẫn đúng

Quầy A dùng web, quầy B dùng app desktop, cùng bấm đặt khung 09:00 của một bác sĩ. Cả hai gọi cùng `AppointmentService.CreateAppointmentAsync` → cùng transaction Serializable → cùng unique index `UX_Appointment_ActiveDoctorStart`. **Chỉ một người thành công**, người kia nhận "Khung giờ này vừa được đặt". Test `Toan_luong_tu_dat_lich_den_cap_thuoc_qua_ViewModel` tái hiện đúng cảnh hai quầy tranh một khung (hai `BookingViewModel` riêng biệt).

## B4. DTO là hợp đồng giữa B và C, ở cả hai bản

`AppointmentListItemDto`, `SlotDto`, `PatientListItemDto`... là **cùng một hợp đồng** mà web ViewModel và desktop ViewModel dùng. Khi B đổi Entity mà giữ nguyên DTO, cả hai giao diện không phải sửa.

---

# PHẦN D — GHÉP A + B + C: THEO DẤU NÚT "XÁC NHẬN LỊCH" TRONG APP DESKTOP

```text
[C·View]  Tiếp tân bấm nút "Xác nhận lịch" (BookingView.xaml)
            │ Command="{Binding ConfirmCommand}"
            ▼
[C·VM]    BookingViewModel.ConfirmAsync -> RunBusyAsync (IsBusy = true, thanh tiến trình chạy)
            │ chụp lựa chọn, đổi 08:00 giờ VN -> 01:00 UTC
            │ runner.RunAsync((IAppointmentService s) => s.CreateAppointmentAsync(dto))
            ▼
[A]       ScopedServiceRunner: tạo scope mới -> DI tạo AppointmentService + ClinicDbContext MỚI
            │ ICurrentUserContext = UserSession (tiếp tân đang đăng nhập)
            ▼
[Service] PermissionGuard kiểm vai trò -> kiểm giờ hợp lệ -> transaction Serializable
            │ -> kiểm chồng lấn -> INSERT -> nhật ký -> COMMIT
            ▼
[B]       ClinicDbContext, Entity Appointment, unique index -> SQL Server
            ▲ ServiceResult
[A]       scope hủy, DbContext hủy
[C·VM]    Succeeded -> SuccessMessage (nêu rõ mã bé) ; thất bại -> ErrorMessage
            │ ReloadSlotsAsync -> Slots cập nhật
            ▼
[C·View]  MessageBar hiện thông báo xanh/đỏ; khung vừa đặt chuyển "Đã đặt" và bị khóa
```

## D2. Bằng chứng: 21 test, chạy trên Mac

📁 [tests/ClinicManagementSystem.Desktop.Tests](../tests/ClinicManagementSystem.Desktop.Tests/ViewModelTests.cs)

| Nhóm | Số test | Kiểm tra gì |
|---|---|---|
| Unit test ViewModel ([ViewModelTests.cs](../tests/ClinicManagementSystem.Desktop.Tests/ViewModelTests.cs)) | 15 | Đặt lịch, check-in, đăng nhập, menu theo vai trò, với service giả |
| Kiểm tra "dây nối" ([WiringTests.cs](../tests/ClinicManagementSystem.Desktop.Tests/WiringTests.cs)) | 4 | DI đăng ký đủ và đúng vòng đời; mọi trang có View; **mọi binding trong XAML trỏ tới thuộc tính có thật**; mọi `StaticResource` đã khai báo |
| Toàn luồng với SQL Server thật ([EndToEndTests.cs](../tests/ClinicManagementSystem.Desktop.Tests/EndToEndTests.cs)) | 2 | Tiếp tân đặt lịch (hai quầy tranh một khung) → check-in → bác sĩ khám, kê đơn → thu ngân thu tiền → dược sĩ cấp thuốc; bác sĩ mở trang đặt lịch bị tầng Services chặn |

> ❓ **"Vì sao cần test 'dây nối' XAML?"** — Gõ sai tên trong `{Binding Tenn}` thì trình biên dịch **không báo** và WPF cũng không sập, ô đó chỉ hiện **trống**. Test này đọc mọi file XAML và đối chiếu từng tên với thuộc tính thật của ViewModel, bắt lỗi gõ sai mà không cần mở cửa sổ.

**Điều test không bao phủ** (nói thẳng nếu thầy hỏi): giao diện chưa được mở trên Windows, nên bố cục, màu và cách hiển thị thực tế chưa được nhìn tận mắt. Logic phía sau đã được chạy thật.

---

# PHẦN E — CÂU HỎI VẤN ĐÁP MVVM

**Kiến thức nền**

1. *MVVM gồm những gì, mỗi phần làm gì trong app này?* → Model = DTO + Service (nghiệp vụ, dữ liệu); View = XAML (hiển thị, không logic); ViewModel = trạng thái + Command của một màn hình, không biết View.
2. *Data binding hoạt động nhờ cái gì?* → `DataContext` + `{Binding}`; ViewModel cài `INotifyPropertyChanged` để báo khi thuộc tính đổi; `[ObservableProperty]` sinh sẵn phần này.
3. *ICommand và CanExecute giúp gì?* → Nút gọi lệnh của ViewModel thay vì gọi hàm trong code-behind; `CanExecute` false thì nút tự mờ; lệnh async tự chặn bấm hai lần.
4. *Vì sao dùng ObservableCollection thay cho List?* → Thêm/xóa phần tử thì giao diện tự cập nhật (lưới giờ, bảng thuốc).
5. *Source generator của CommunityToolkit.Mvvm làm gì? Vì sao cần `partial`?* → Lúc biên dịch tự viết thuộc tính có báo thay đổi, lệnh RelayCommand, các móc OnXxxChanged; `partial` cho phép phần code sinh ra được ghép vào cùng lớp.

**Kiến trúc (phần A)**

6. *Vì sao ViewModel nằm ở project `net10.0` riêng?* → Không dùng được WPF nên giữ đúng kỷ luật MVVM; test chạy được trên mọi máy.
7. *DbContext được tạo và hủy khi nào trong app desktop?* → Mỗi thao tác một scope qua `IServiceRunner`; tránh dữ liệu cũ và lỗi đa luồng; tương đương "mỗi request một DbContext" của web.
8. *Captive dependency là gì? App chống nó thế nào?* → Singleton giữ đối tượng Scoped (DbContext) nên nó sống mãi; chống bằng `IServiceRunner` và `ValidateScopes/ValidateOnBuild` khi khởi động.
9. *Điều hướng giữa các trang làm thế nào?* → `Navigator.CurrentPage` + `ContentControl` + DataTemplate trong `ViewMappings.xaml`; ViewModel-first, truyền tham số bằng `configure`.
10. *ViewModel hỏi lại người dùng mà không gọi MessageBox bằng cách nào?* → Qua `IDialogService`; WPF cài bản thật, test cài bản giả.

**Nghiệp vụ (phần C)**

11. *Đổi bác sĩ thì lưới giờ tải lại nhờ đâu?* → Móc `partial void OnSelectedDoctorChanged` do bộ sinh mã gọi, chạy `LoadSlotsCommand`.
12. *Nút "Xác nhận lịch" đã mờ khi khung bận, sao Service còn kiểm tra?* → Dữ liệu trên màn hình có thể cũ; quầy khác có thể vừa đặt; Service + database mới là trọng tài (3 lớp chống trùng).
13. *Kiểm tra ô nhập ở desktop khác web ở đâu?* → Cùng `[Required]`/`[Range]`; desktop báo ngay khi gõ nhờ `ObservableValidator` + `INotifyDataErrorInfo`; vẫn `ValidateAllProperties()` trước khi lưu.
14. *Vì sao có lớp `AppointmentRow` mà không bind thẳng DTO?* → Chuẩn bị chữ hiển thị (giờ VN, SĐT có dấu cách, nhãn trạng thái, `CanAct`), để XAML không có logic và test kiểm được.

**Câu "bẫy"**

15. *"MVVM thì code-behind phải trống hoàn toàn, LoginView sai rồi?"* → **Không.** MVVM cấm logic nghiệp vụ trong code-behind, không cấm code hiển thị. `PasswordBox` không bind được nên code-behind chỉ chép giá trị.
16. *"App desktop dùng lại Services thì đâu cần kiểm thử lại nghiệp vụ?"* → **Nửa đúng.** Quy tắc nghiệp vụ đã có test riêng, nhưng **cách ViewModel gọi** chúng (giờ UTC, giữ lựa chọn khi lỗi, scope DbContext) vẫn phải test. Vì vậy có 21 test riêng của desktop.
17. *"Ẩn menu Đặt lịch với bác sĩ là đủ bảo mật?"* → **Không.** Test `Bac_si_goi_thang_ViewModel_dat_lich_van_bi_tang_Services_chan` chứng minh: kể cả mở được trang, `PermissionGuard` vẫn chặn và ViewModel hiện lỗi thay vì sập.
18. *"Build trên Mac thành công là app chạy được trên Windows?"* → **Không chắc.** Build kiểm tra cú pháp C# và XAML; test kiểm logic và dây nối. Riêng giao diện thật chỉ kiểm chứng được khi mở trên Windows.
