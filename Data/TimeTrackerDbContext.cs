using Microsoft.EntityFrameworkCore;
using TimeTracker.Models;

namespace TimeTracker.Data;

public class TimeTrackerDbContext : DbContext
{
    public TimeTrackerDbContext(DbContextOptions<TimeTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    // The tenant for this context. Set per web request by TenantMiddleware, which rejects requests without a known user.
    // When null (tests, startup work) no tenant filtering is applied.
    public int? CurrentUserId { get; set; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AssignTenant()
    {
        if (CurrentUserId is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            switch (entry.Entity)
            {
                case Customer { UserId: null } customer:
                    customer.UserId = CurrentUserId;
                    break;
                case Project { UserId: null } project:
                    project.UserId = CurrentUserId;
                    break;
                case TimeEntry { UserId: null } timeEntry:
                    timeEntry.UserId = CurrentUserId;
                    break;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Login).IsRequired().HasMaxLength(320);
            entity.HasIndex(u => u.Login).IsUnique();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(c => c.Projects)
                .WithOne(p => p.Customer)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(c => CurrentUserId == null || c.UserId == CurrentUserId);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(p => p.TimeEntries)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(p => CurrentUserId == null || p.UserId == CurrentUserId);
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.StartTime).IsRequired();
            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(t => CurrentUserId == null || t.UserId == CurrentUserId);
        });
    }
}
