using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Upgrade> Upgrades => Set<Upgrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Upgrade>(entity =>
        {
            entity.ToTable(u => u.HasCheckConstraint("CK_Upgrade_Priority", "Priority BETWEEN 1 AND 100"));

            entity.HasOne(u => u.PrerequisiteUpgrade)
                .WithMany(u => u.DependentUpgrades)
                .HasForeignKey(u => u.PrerequisiteUpgradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges()
    {
        TouchTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void TouchTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Upgrade>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
