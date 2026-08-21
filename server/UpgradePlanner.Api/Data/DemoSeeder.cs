using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Data;

/// <summary>
/// Writes <see cref="DemoData"/> into a database.
/// </summary>
/// <remarks>
/// <para>
/// Filling an empty database and wiping a populated one are <b>two separately
/// named operations</b>, never one function with a <c>bool overwrite</c>
/// parameter. A call site cannot ask for <see cref="SeedIfEmpty"/> and get
/// <see cref="ResetToDemoData"/> by passing the wrong argument, because there is
/// no argument to get wrong.
/// </para>
/// <para>
/// This replaces an earlier <c>Reseed</c> that deleted every row unconditionally
/// whenever an environment variable was set. Startup now calls only
/// <see cref="SeedIfEmpty"/>; the destructive path is reachable exclusively from
/// the explicit <c>--reset-demo-data</c> command line flag.
/// </para>
/// </remarks>
public static class DemoSeeder
{
    /// <summary>
    /// Inserts the demo fixture <b>only if the table is empty</b>, and returns
    /// how many rows were written (zero when data was already present).
    /// </summary>
    /// <remarks>
    /// This method never deletes and never updates. Pointing it at a populated
    /// database — including the author's real one — is a no-op, which is what
    /// makes it safe to run unconditionally at startup.
    /// </remarks>
    public static int SeedIfEmpty(AppDbContext db, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (db.Upgrades.Any())
        {
            return 0;
        }

        return Insert(db, now);
    }

    /// <summary>
    /// Deletes every upgrade and replaces them with a fresh demo fixture.
    /// <b>Destructive.</b>
    /// </summary>
    /// <remarks>
    /// Only ever invoked from the <c>--reset-demo-data</c> flag, and only after
    /// <c>Program</c> has confirmed the process is in demo mode — which forces
    /// the connection string to <see cref="Configuration.AppOptions.DemoConnectionString"/>,
    /// so this cannot run against the real database.
    /// </remarks>
    public static int ResetToDemoData(AppDbContext db, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(db);

        // Children reference parents with DeleteBehavior.Restrict, so clear the
        // links before removing rows or SQLite rejects the delete.
        foreach (var upgrade in db.Upgrades)
        {
            upgrade.PrerequisiteUpgradeId = null;
        }
        db.SaveChanges();

        db.Upgrades.RemoveRange(db.Upgrades);
        db.SaveChanges();

        return Insert(db, now);
    }

    private static int Insert(AppDbContext db, DateTime now)
    {
        var rows = DemoData.Build(now);
        var idsByKey = new Dictionary<string, int>(rows.Count);

        // Insert parents before children so a prerequisite id always exists by the
        // time something points at it. DemoData lists each row after its own
        // prerequisite, so a single forward pass is enough; the check below turns a
        // future ordering mistake into a clear failure rather than a null link.
        foreach (var row in rows)
        {
            if (row.PrerequisiteKey is not null)
            {
                if (!idsByKey.TryGetValue(row.PrerequisiteKey, out var prerequisiteId))
                {
                    throw new InvalidOperationException(
                        $"Demo fixture row '{row.Key}' lists prerequisite '{row.PrerequisiteKey}', "
                        + "which is not defined before it in DemoData.Build.");
                }

                row.Entity.PrerequisiteUpgradeId = prerequisiteId;
            }

            db.Upgrades.Add(row.Entity);
            db.SaveChanges();
            idsByKey[row.Key] = row.Entity.Id;
        }

        return rows.Count;
    }

    /// <summary>Row count, for logging how a seed decision turned out.</summary>
    public static int CountUpgrades(AppDbContext db) => db.Upgrades.Count();
}
