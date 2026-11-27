using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Data;

public class LeaveDbContext(DbContextOptions<LeaveDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(254);
            e.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(254);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.HasOne(x => x.Manager)
                .WithMany()
                .HasForeignKey(x => x.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveRequest>(r =>
        {
            r.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            r.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            r.Property(x => x.Reason).HasMaxLength(500);
            r.Property(x => x.DecisionComment).HasMaxLength(500);
            // SQLite has no time zone: values are written in UTC, so mark them as UTC when read back
            // (otherwise JSON omits the 'Z'). Fixes LT-001 review item 1.
            r.Property(x => x.DecidedAtUtc).HasConversion(
                v => v,
                v => v == null ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
            r.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.DecidedBy)
                .WithMany()
                .HasForeignKey(x => x.DecidedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
