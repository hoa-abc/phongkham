using ClinicManagementSystem.DataAccess.Entities.Appointments;
using ClinicManagementSystem.DataAccess.Entities.Auditing;
using ClinicManagementSystem.DataAccess.Entities.Billing;
using ClinicManagementSystem.DataAccess.Entities.Clinical;
using ClinicManagementSystem.DataAccess.Entities.Identity;
using ClinicManagementSystem.DataAccess.Entities.Patients;
using ClinicManagementSystem.DataAccess.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementSystem.DataAccess;

/// <summary>
/// DbContext = "phiên làm việc" với database. Nó làm 3 việc:
///  1. DbSet&lt;T&gt;: cửa ngõ truy vấn từng bảng bằng LINQ (EF Core dịch LINQ thành SQL).
///  2. Change Tracker: nhớ entity nào đã bị sửa để SaveChanges sinh đúng INSERT/UPDATE/DELETE.
///  3. OnModelCreating: mô tả bảng, khóa, index, ràng buộc -> từ đó sinh migration (Code-First).
/// Trong web, mỗi HTTP request có một DbContext riêng (Scoped) và bị hủy khi request kết thúc.
/// </summary>
public sealed class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Allergy> Allergies => Set<Allergy>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<VisitServiceOrder> VisitServiceOrders => Set<VisitServiceOrder>();
    public DbSet<ClinicService> ClinicServices => Set<ClinicService>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PrescriptionStatusHistory> PrescriptionStatusHistory => Set<PrescriptionStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Dispense> Dispenses => Set<Dispense>();
    public DbSet<DispenseItem> DispenseItems => Set<DispenseItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tự tìm mọi lớp IEntityTypeConfiguration<T> trong thư mục Configurations/.
        // Mỗi phân hệ có file cấu hình riêng -> các thành viên nhóm không sửa chung một file.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicDbContext).Assembly);
    }
}
