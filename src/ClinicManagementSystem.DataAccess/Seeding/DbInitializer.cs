using ClinicManagementSystem.Common.Enums;
using ClinicManagementSystem.Common.Formatting;
using ClinicManagementSystem.Common.Security;
using ClinicManagementSystem.Common.Time;
using ClinicManagementSystem.DataAccess.Entities.Appointments;
using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.DataAccess.Entities.Clinical;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using ClinicManagementSystem.DataAccess.Entities.Patients;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.DataAccess.Seeding;

/// <summary>
/// Chạy lúc khởi động (môi trường Development): áp migration rồi nạp dữ liệu MẪU GIẢ LẬP.
/// Tính chất quan trọng: IDEMPOTENT - chạy bao nhiêu lần cũng không tạo trùng dữ liệu.
/// Toàn bộ tên, số điện thoại ở đây là bịa để trình diễn; không dùng hồ sơ sức khỏe thật.
/// </summary>
public sealed class DbInitializer(ClinicDbContext db)
{
    public async Task InitializeAsync(string demoPassword, CancellationToken ct = default)
    {
        // MigrateAsync: tạo database nếu chưa có và chạy các migration còn thiếu.
        // (Không dùng EnsureCreated vì nó bỏ qua cơ chế migration của Code-First.)
        await db.Database.MigrateAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            await SeedBaseDataAsync(demoPassword, ct);
        }

        await EnsureTodayAppointmentsAsync(ct);
    }

    private async Task SeedBaseDataAsync(string demoPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(demoPassword) || demoPassword.Length < 8)
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình Seed:DemoPassword (tối thiểu 8 ký tự) trong appsettings.Development.json.");
        }

        // Một transaction cho toàn bộ seed: hoặc có đủ dữ liệu mẫu, hoặc không có gì.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        // ----- 1. Tài khoản: mỗi tài khoản có salt riêng nên hash khác nhau dù chung mật khẩu demo -----
        User NewUser(string userName, string displayName, UserRole role, string? specialty = null) => new()
        {
            UserName = userName,
            DisplayName = displayName,
            Role = role,
            Specialty = specialty,
            PasswordHash = PasswordHasher.Hash(demoPassword),
            IsActive = true,
            CreatedAtUtc = now
        };

        var admin = NewUser("admin", "Quản trị viên Demo", UserRole.Admin);
        var receptionist = NewUser("letan", "Tiếp tân Demo", UserRole.Receptionist);
        var doctors = new[]
        {
            NewUser("bacsi", "BS. Võ Tuấn Kiệt", UserRole.Doctor, "Nhi Tổng quát - Phòng 1"),
            NewUser("bacsi2", "BS. Trần Thị Bình", UserRole.Doctor, "Tai Mũi Họng - Phòng 2"),
            NewUser("bacsi3", "BS. Nguyễn Duy Khánh", UserRole.Doctor, "Nhi Hô hấp - Phòng 3")
        };
        var pharmacist = NewUser("duocsi", "Dược sĩ Demo", UserRole.Pharmacist);
        db.Users.AddRange(admin, receptionist, pharmacist);
        db.Users.AddRange(doctors);
        await db.SaveChangesAsync(ct); // lưu trước để có UserId dùng làm khóa ngoại bên dưới

        // ----- 2. Danh mục dịch vụ (giá minh họa) -----
        var generalExam = new ClinicService { Code = "PED-GEN", Name = "Khám nhi tổng quát", Price = 150_000m };
        db.ClinicServices.AddRange(
            generalExam,
            new ClinicService { Code = "PED-FOLLOWUP", Name = "Tái khám nhi", Price = 100_000m },
            new ClinicService { Code = "PED-NUTRITION", Name = "Tư vấn dinh dưỡng", Price = 180_000m },
            new ClinicService { Code = "LAB-CBC", Name = "Xét nghiệm công thức máu", Price = 120_000m });

        // ----- 3. Danh mục thuốc: cố ý có thuốc hết hàng và thuốc sắp hết để trình diễn cảnh báo -----
        Medicine NewMedicine(string code, string name, string ingredient, string? group, string strength,
            string form, string unit, decimal price, decimal stock, decimal reorder) => new()
        {
            Code = code,
            Name = name,
            ActiveIngredient = ingredient,
            ActiveIngredientKey = CodeFormatter.NormalizeKey(ingredient),
            TherapeuticGroupKey = group is null ? null : CodeFormatter.NormalizeKey(group),
            Strength = strength,
            DosageForm = form,
            Unit = unit,
            UnitPrice = price,
            StockQuantity = stock,
            ReorderLevel = reorder
        };

        var medicines = new[]
        {
            NewMedicine("MED-PAR", "Hỗn dịch Paracetamol", "Paracetamol", null, "120mg/5ml", "Hỗn dịch uống", "chai", 35_000m, 50, 10),
            NewMedicine("MED-AMX", "Amoxicillin bột pha hỗn dịch", "Amoxicillin", "Penicillin", "250mg", "Bột pha uống", "gói", 3_000m, 200, 30),
            NewMedicine("MED-IBU", "Hỗn dịch Ibuprofen", "Ibuprofen", "NSAID", "100mg/5ml", "Hỗn dịch uống", "chai", 42_000m, 0, 5),
            NewMedicine("MED-ORS", "Oresol", "Oral rehydration salts", null, "27.9g", "Bột pha uống", "gói", 4_000m, 300, 50),
            NewMedicine("MED-PRO", "Men tiêu hóa Bacillus clausii", "Bacillus clausii", null, "2 tỷ/5ml", "Ống uống", "ống", 8_000m, 8, 20),
            NewMedicine("MED-VTC", "Vitamin C", "Ascorbic acid", null, "100mg", "Viên nén", "viên", 1_500m, 500, 100),
            NewMedicine("MED-SAL", "Nước muối sinh lý xịt mũi", "Sodium chloride", null, "0.9%", "Dung dịch xịt", "chai", 25_000m, 40, 10),
            NewMedicine("MED-CET", "Siro Cetirizine", "Cetirizine", null, "5mg/5ml", "Siro", "chai", 38_000m, 25, 5)
        };
        db.Medicines.AddRange(medicines);
        await db.SaveChangesAsync(ct);

        // Tồn đầu kỳ cũng phải có dòng sổ kho, để "tổng các giao dịch = tồn hiện tại".
        db.InventoryTransactions.AddRange(medicines.Where(m => m.StockQuantity > 0).Select(m => new InventoryTransaction
        {
            MedicineId = m.MedicineId,
            QuantityChange = m.StockQuantity,
            Type = InventoryTransactionType.OpeningBalance,
            Reference = "Tồn đầu kỳ (dữ liệu mẫu)",
            OccurredAtUtc = now,
            PerformedByUserId = admin.UserId
        }));

        // ----- 4. Người giám hộ và bệnh nhi -----
        var guardians = new[]
        {
            new Guardian { FullName = "Nguyễn Văn An", PhoneNumber = "0901234567", Address = "Quận 1, TP. Hồ Chí Minh" },
            new Guardian { FullName = "Trần Văn Bảo", PhoneNumber = "0987654321", Address = "Quận 3, TP. Hồ Chí Minh" },
            new Guardian { FullName = "Lê Thu Hương", PhoneNumber = "0912345678", Address = "Thành phố Thủ Đức" },
            new Guardian { FullName = "Phạm Thị Mai", PhoneNumber = "0933111222", Address = "Quận Bình Thạnh" },
            new Guardian { FullName = "Hoàng Minh Đức", PhoneNumber = "0977000111", Address = "Quận 7, TP. Hồ Chí Minh" },
            new Guardian { FullName = "Đặng Thị Lan", PhoneNumber = "0966222333", Address = "Quận Gò Vấp" }
        };

        // (tên bé, ngày sinh, giới tính, vị trí phụ huynh, số tháng trước đây hồ sơ được tạo)
        var patientSeeds = new (string Name, DateOnly Dob, Gender Gender, int Guardian, int MonthsAgo)[]
        {
            ("Nguyễn Văn Tèo", new DateOnly(2022, 4, 15), Gender.Male, 0, 5),
            ("Trần Thị Nụ", new DateOnly(2021, 8, 22), Gender.Female, 1, 5),
            ("Lê Phương Hoa", new DateOnly(2024, 12, 1), Gender.Female, 2, 4),
            ("Nguyễn Thị Na", new DateOnly(2023, 2, 9), Gender.Female, 0, 4),
            ("Phạm Gia Huy", new DateOnly(2020, 6, 30), Gender.Male, 3, 3),
            ("Phạm Bảo Ngọc", new DateOnly(2025, 3, 12), Gender.Female, 3, 3),
            ("Hoàng Minh Khôi", new DateOnly(2021, 11, 5), Gender.Male, 4, 2),
            ("Đặng Tuệ Nhi", new DateOnly(2023, 9, 18), Gender.Female, 5, 2),
            ("Đặng Quốc Bảo", new DateOnly(2019, 1, 25), Gender.Male, 5, 1),
            ("Trần Minh Quân", new DateOnly(2025, 7, 2), Gender.Male, 1, 0)
        };

        var patients = new List<Patient>();
        foreach (var guardian in guardians)
        {
            guardian.CreatedAtUtc = now.AddMonths(-5);
        }

        foreach (var seed in patientSeeds)
        {
            patients.Add(new Patient
            {
                Guardian = guardians[seed.Guardian],
                GuardianRelationship = GuardianRelationship.Parent,
                MedicalRecordNumber = $"TMP-{Guid.NewGuid():N}", // mã tạm, đổi ngay sau khi có PatientId
                FullName = seed.Name,
                DateOfBirth = seed.Dob,
                Gender = seed.Gender,
                CreatedAtUtc = seed.MonthsAgo == 0 ? now.AddDays(-1) : now.AddMonths(-seed.MonthsAgo)
            });
        }

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);
        foreach (var patient in patients)
        {
            patient.MedicalRecordNumber = CodeFormatter.PatientCode(patient.PatientId);
        }

        // Dị ứng mẫu: bé Tèo dị ứng nhóm Penicillin -> kê Amoxicillin sẽ hiện CẢNH BÁO ĐỎ.
        db.Allergies.AddRange(
            new Allergy
            {
                PatientId = patients[0].PatientId, DisplayName = "Penicillin", AllergenKey = "PENICILLIN",
                Severity = AllergySeverity.Severe, Reaction = "Nổi mề đay toàn thân (dữ liệu mẫu)", RecordedAtUtc = now
            },
            new Allergy
            {
                PatientId = patients[1].PatientId, DisplayName = "Hải sản", AllergenKey = "HẢI SẢN",
                Severity = AllergySeverity.Mild, Reaction = "Ngứa nhẹ (dữ liệu mẫu)", RecordedAtUtc = now
            });
        await db.SaveChangesAsync(ct);

        // ----- 5. Lịch sử khám 6 tháng gần đây để trang Tổng quan có biểu đồ -----
        SeedHistory(patients, doctors, receptionist, generalExam);
        await db.SaveChangesAsync(ct);

        // Số hóa đơn phụ thuộc VisitId nên gán sau khi đã lưu.
        var seededInvoices = await db.Invoices.Where(x => x.InvoiceNumber.StartsWith("TMP-")).ToListAsync(ct);
        foreach (var invoice in seededInvoices)
        {
            invoice.InvoiceNumber = CodeFormatter.InvoiceNumber(invoice.VisitId, 1);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void SeedHistory(List<Patient> patients, User[] doctors, User receptionist, ClinicService exam)
    {
        var today = ClinicClock.Today;
        var diagnoses = new[]
        {
            "Viêm họng cấp", "Sốt siêu vi", "Viêm mũi dị ứng", "Rối loạn tiêu hóa", "Khám sức khỏe định kỳ"
        };
        var sequence = 0;

        for (var monthsAgo = 5; monthsAgo >= 0; monthsAgo--)
        {
            var monthStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-monthsAgo);
            var visitsThisMonth = 5 + (5 - monthsAgo) * 2; // tăng dần để biểu đồ có xu hướng

            for (var i = 0; i < visitsThisMonth; i++)
            {
                // Mỗi lượt khám mẫu trong tháng rơi vào MỘT NGÀY RIÊNG: vì 17 và 28 nguyên tố cùng nhau nên
                // (i * 17) % 28 cho 28 giá trị khác nhau với i = 0..27 (mỗi tháng tối đa 15 lượt).
                // Khác ngày thì không bao giờ trùng (bác sĩ, giờ bắt đầu) -> không vi phạm unique index.
                // (Cách cũ (i * 3) % 27 lặp lại ngày khi i = 9 và có thể trùng giờ tùy ngày chạy seed.)
                var day = monthStart.AddDays((i * 17) % 28);
                if (day >= today)
                {
                    continue; // chỉ tạo lịch sử TRƯỚC hôm nay
                }

                var doctor = doctors[sequence % doctors.Length];
                var patient = patients[sequence % patients.Count];
                var startLocal = day.ToDateTime(new TimeOnly(8 + (sequence % 3), (sequence % 2) * 30));
                var startUtc = ClinicClock.ToUtc(startLocal);
                var doneUtc = startUtc.AddMinutes(40);

                db.Visits.Add(new Visit
                {
                    Status = VisitStatus.Completed,
                    ChiefComplaint = "Khám theo lịch hẹn (dữ liệu mẫu)",
                    Diagnosis = diagnoses[sequence % diagnoses.Length],
                    TreatmentPlan = "Theo dõi tại nhà, tái khám khi cần.",
                    CheckedInAtUtc = startUtc.AddMinutes(-10),
                    StartedAtUtc = startUtc,
                    CompletedAtUtc = doneUtc,
                    Appointment = new Appointment
                    {
                        PatientId = patient.PatientId,
                        DoctorId = doctor.UserId,
                        BookedByUserId = receptionist.UserId,
                        StartsAtUtc = startUtc,
                        EndsAtUtc = startUtc.AddMinutes(30),
                        Status = AppointmentStatus.Completed,
                        CreatedAtUtc = startUtc.AddDays(-2)
                    },
                    VitalSigns =
                    {
                        new VitalSign
                        {
                            WeightKg = 12 + sequence % 9, HeightCm = 85 + sequence % 30, TemperatureC = 36.8m,
                            HeartRate = 95 + sequence % 15, RecordedAtUtc = startUtc.AddMinutes(-10),
                            RecordedByUserId = receptionist.UserId
                        }
                    },
                    ServiceOrders =
                    {
                        new VisitServiceOrder
                        {
                            ServiceId = exam.ServiceId, Quantity = 1, UnitPriceSnapshot = exam.Price,
                            OrderedByUserId = doctor.UserId, OrderedAtUtc = startUtc
                        }
                    },
                    Invoices =
                    {
                        new Invoice
                        {
                            InvoiceNumber = $"TMP-{Guid.NewGuid():N}",
                            TotalAmount = exam.Price,
                            Status = InvoiceStatus.Paid,
                            IsLocked = true,
                            CreatedAtUtc = doneUtc,
                            PaidAtUtc = doneUtc.AddMinutes(5),
                            CreatedByUserId = doctor.UserId,
                            Lines =
                            {
                                new InvoiceLine
                                {
                                    Description = exam.Name, Quantity = 1, UnitLabel = "lần",
                                    UnitPrice = exam.Price, LineTotal = exam.Price
                                }
                            },
                            Payments =
                            {
                                new Payment
                                {
                                    Amount = exam.Price, Method = PaymentMethod.Cash, Status = PaymentStatus.Succeeded,
                                    PaidAtUtc = doneUtc.AddMinutes(5), ReceivedByUserId = receptionist.UserId
                                }
                            }
                        }
                    }
                });
                sequence++;
            }
        }
    }

    /// <summary>
    /// Mỗi ngày mở ứng dụng để demo đều có sẵn vài lịch hẹn "hôm nay" ở trạng thái Đã đặt.
    /// Nếu hôm nay đã có lịch (do seed trước đó hoặc do người dùng đặt) thì không làm gì.
    /// </summary>
    private async Task EnsureTodayAppointmentsAsync(CancellationToken ct)
    {
        var today = ClinicClock.Today;
        var (startUtc, endUtc) = ClinicClock.DayRangeUtc(today);
        if (await db.Appointments.AnyAsync(x => x.StartsAtUtc >= startUtc && x.StartsAtUtc < endUtc, ct))
        {
            return;
        }

        var doctors = await db.Users.Where(x => x.Role == UserRole.Doctor && x.IsActive)
            .OrderBy(x => x.UserId).ToListAsync(ct);
        var patients = await db.Patients.Where(x => x.IsActive).OrderBy(x => x.PatientId).Take(4).ToListAsync(ct);
        var bookedBy = await db.Users.Where(x => x.Role == UserRole.Receptionist)
            .OrderBy(x => x.UserId).Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct);
        if (doctors.Count == 0 || patients.Count == 0 || bookedBy is null)
        {
            return;
        }

        var times = new[] { new TimeOnly(8, 0), new TimeOnly(9, 30), new TimeOnly(10, 0), new TimeOnly(14, 0) };
        for (var i = 0; i < patients.Count; i++)
        {
            var start = ClinicClock.ToUtc(today.ToDateTime(times[i]));
            db.Appointments.Add(new Appointment
            {
                PatientId = patients[i].PatientId,
                DoctorId = doctors[i == 2 && doctors.Count > 1 ? 1 : 0].UserId,
                BookedByUserId = bookedBy.Value,
                StartsAtUtc = start,
                EndsAtUtc = start.AddMinutes(30),
                Reason = "Khám theo lịch hẹn (dữ liệu mẫu)",
                Status = AppointmentStatus.Booked,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
