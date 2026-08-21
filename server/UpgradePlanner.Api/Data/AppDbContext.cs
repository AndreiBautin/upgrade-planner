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

    /// <summary>
    /// Stamps <see cref="Upgrade.CreatedAt"/> and <see cref="Upgrade.UpdatedAt"/> at the
    /// persistence boundary, so no call site can forget them.
    /// </summary>
    /// <remarks>
    /// On insert the timestamps are only filled in when they are still
    /// <see langword="default"/>. That leaves a caller that supplied its own
    /// history — the demo fixture, which backdates rows relative to seed time —
    /// with the dates it asked for, while an ordinary insert that sets nothing
    /// still cannot end up with a zero timestamp. Updates always re-stamp
    /// <see cref="Upgrade.UpdatedAt"/>: "when did this last change" is the
    /// database's answer to give, not the caller's.
    /// </remarks>
    private void TouchTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Upgrade>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                if (entry.Entity.UpdatedAt == default) entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
