using UpgradePlanner.Api.Data;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Tests;

/// <summary>
/// The seeding guarantees, tested from the side that must not happen.
/// </summary>
/// <remarks>
/// The predecessor of this code deleted every row whenever an environment
/// variable was set, so <see cref="SeedIfEmpty_never_overwrites_existing_data"/>
/// is the single most important test in the suite: it is the one standing
/// between a mis-set variable and the author's real database.
/// </remarks>
public class DemoSeederTests : IDisposable
{
    private readonly TestDatabase _fixture = new();
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _fixture.Dispose();

    private Upgrade GivenExistingRow(string title = "My real upgrade")
    {
        var existing = new Upgrade
        {
            Title = title,
            Priority = 42,
            Category = UpgradeCategory.Home,
            Status = UpgradeStatus.Researching,
        };
        _fixture.Db.Upgrades.Add(existing);
        _fixture.Db.SaveChanges();
        return existing;
    }

    // --- Seed only into empty storage ----------------------------------------

    [Fact]
    public void SeedIfEmpty_fills_an_empty_database()
    {
        var written = DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        Assert.True(written > 0);
        Assert.Equal(written, _fixture.Db.Upgrades.Count());
    }

    [Fact]
    public void SeedIfEmpty_never_overwrites_existing_data()
    {
        var existing = GivenExistingRow();

        var written = DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        Assert.Equal(0, written);
        Assert.Equal(1, _fixture.Db.Upgrades.Count());

        var survivor = _fixture.Db.Upgrades.Single();
        Assert.Equal(existing.Id, survivor.Id);
        Assert.Equal("My real upgrade", survivor.Title);
        Assert.Equal(42, survivor.Priority);
    }

    [Fact]
    public void SeedIfEmpty_is_a_no_op_even_when_only_one_unrelated_row_exists()
    {
        GivenExistingRow("Just one thing");

        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);
        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);
        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        Assert.Equal(1, _fixture.Db.Upgrades.Count());
    }

    [Fact]
    public void SeedIfEmpty_is_idempotent()
    {
        var first = DemoSeeder.SeedIfEmpty(_fixture.Db, Now);
        var second = DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        Assert.True(first > 0);
        Assert.Equal(0, second);
        Assert.Equal(first, _fixture.Db.Upgrades.Count());
    }

    // --- The destructive operation, which is a different function ------------

    [Fact]
    public void ResetToDemoData_replaces_whatever_was_there()
    {
        GivenExistingRow("Something else");

        var written = DemoSeeder.ResetToDemoData(_fixture.Db, Now);

        Assert.True(written > 0);
        Assert.Equal(written, _fixture.Db.Upgrades.Count());
        Assert.DoesNotContain(_fixture.Db.Upgrades, u => u.Title == "Something else");
    }

    [Fact]
    public void ResetToDemoData_works_on_an_already_seeded_database()
    {
        // Prerequisite links use DeleteBehavior.Restrict, so a naive delete of a
        // seeded set fails on the foreign key. This is the regression guard.
        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        var written = DemoSeeder.ResetToDemoData(_fixture.Db, Now);

        Assert.True(written > 0);
        Assert.Equal(written, _fixture.Db.Upgrades.Count());
    }

    [Fact]
    public void ResetToDemoData_leaves_the_same_content_as_a_fresh_seed()
    {
        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);
        var afterSeed = _fixture.Db.Upgrades.Select(u => u.Title).OrderBy(t => t).ToList();

        DemoSeeder.ResetToDemoData(_fixture.Db, Now);
        var afterReset = _fixture.Db.Upgrades.Select(u => u.Title).OrderBy(t => t).ToList();

        Assert.Equal(afterSeed, afterReset);
    }

    // --- The seeded graph has to actually be a graph --------------------------

    [Fact]
    public void Seeding_resolves_prerequisite_links_to_real_rows()
    {
        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        var linked = _fixture.Db.Upgrades.Where(u => u.PrerequisiteUpgradeId != null).ToList();
        var allIds = _fixture.Db.Upgrades.Select(u => u.Id).ToHashSet();

        Assert.NotEmpty(linked);
        foreach (var upgrade in linked)
        {
            Assert.Contains(upgrade.PrerequisiteUpgradeId!.Value, allIds);
            Assert.NotEqual(upgrade.Id, upgrade.PrerequisiteUpgradeId!.Value);
        }
    }

    [Fact]
    public void Seeding_preserves_the_backdated_history_the_fixture_asked_for()
    {
        // AppDbContext stamps timestamps on insert, but only when they are unset.
        // If that ever regresses, every seeded row collapses to "created just now"
        // and the demo looks like it was made five seconds ago.
        DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        var oldest = _fixture.Db.Upgrades.Min(u => u.CreatedAt);

        Assert.True(oldest < DateTime.UtcNow.AddDays(-30),
            $"Expected backdated history, but the oldest row was created at {oldest:O}.");
    }

    [Fact]
    public void Seeded_rows_satisfy_the_priority_check_constraint()
    {
        // SQLite enforces CK_Upgrade_Priority, so a fixture priority outside 1..100
        // would throw here rather than silently persisting.
        var written = DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        Assert.Equal(written, _fixture.Db.Upgrades.Count(u => u.Priority >= 1 && u.Priority <= 100));
    }

    [Fact]
    public void CountUpgrades_reports_what_is_actually_stored()
    {
        Assert.Equal(0, DemoSeeder.CountUpgrades(_fixture.Db));

        var written = DemoSeeder.SeedIfEmpty(_fixture.Db, Now);

        Assert.Equal(written, DemoSeeder.CountUpgrades(_fixture.Db));
    }
}
