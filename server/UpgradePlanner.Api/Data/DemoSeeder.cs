using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Data;

// Seeds the public demo deployment with obviously-fake data so visitors have
// something meaningful to look at without ever touching the author's real
// instance. Called unconditionally (not just "if empty") so every cold start
// on the free hosting tier gives back a clean, predictable demo.
public static class DemoSeeder
{
    public static void Reseed(AppDbContext db)
    {
        db.Upgrades.RemoveRange(db.Upgrades);
        db.SaveChanges();

        var now = DateTime.UtcNow;

        var desk = new Upgrade
        {
            Title = "Bigger Desk",
            Description = "Current desk is too small for a second monitor.",
            Category = UpgradeCategory.Office,
            Priority = 70,
            EstimatedCost = 700,
            Status = UpgradeStatus.ReadyToBuy,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Upgrades.Add(desk);
        db.SaveChanges();

        var monitor = new Upgrade
        {
            Title = "Third Monitor",
            Category = UpgradeCategory.Technology,
            Priority = 75,
            EstimatedCost = 250,
            Status = UpgradeStatus.Idea,
            PrerequisiteUpgradeId = desk.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Upgrades.Add(monitor);
        db.SaveChanges();

        var arm = new Upgrade
        {
            Title = "Monitor Arm",
            Category = UpgradeCategory.Technology,
            Priority = 92,
            EstimatedCost = 60,
            Status = UpgradeStatus.Idea,
            PrerequisiteUpgradeId = monitor.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Upgrades.Add(arm);

        db.Upgrades.AddRange(
            new Upgrade
            {
                Title = "Upgrade Office Chair",
                Category = UpgradeCategory.Office,
                Priority = 85,
                EstimatedCost = 800,
                Status = UpgradeStatus.ReadyToBuy,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Upgrade
            {
                Title = "New Couch",
                Category = UpgradeCategory.Home,
                Priority = 55,
                EstimatedCost = 900,
                Status = UpgradeStatus.Researching,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Upgrade
            {
                Title = "Home NAS",
                Category = UpgradeCategory.Technology,
                Priority = 60,
                EstimatedCost = 500,
                Status = UpgradeStatus.Idea,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Upgrade
            {
                Title = "Dashcam",
                Category = UpgradeCategory.Vehicle,
                Priority = 45,
                EstimatedCost = 120,
                Status = UpgradeStatus.Idea,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new Upgrade
            {
                Title = "Adjustable Bench",
                Category = UpgradeCategory.Gym,
                Priority = 50,
                EstimatedCost = 300,
                Status = UpgradeStatus.Purchased,
                PurchasedDate = now.AddDays(-18),
                ActualCost = 275,
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-18),
            }
        );

        db.SaveChanges();
    }
}
