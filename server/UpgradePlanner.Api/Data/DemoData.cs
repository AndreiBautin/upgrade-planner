using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Data;

/// <summary>One fixture row, linked to its prerequisite by key rather than by id.</summary>
/// <remarks>
/// Keys keep <see cref="DemoData.Build"/> a pure function: it can describe the
/// dependency graph without a database round-trip to discover generated ids.
/// <see cref="DemoSeeder"/> resolves keys to ids at insert time.
/// </remarks>
public sealed record DemoUpgrade(string Key, string? PrerequisiteKey, Upgrade Entity);

/// <summary>
/// The public demo dataset.
/// </summary>
/// <remarks>
/// <para>
/// This data is <b>generated, never captured</b>. It is source code, readable by
/// anyone with the repository, and there is no export step from the author's
/// machine anywhere in the pipeline. Nothing here describes a real person, a
/// real purchase, or a real account.
/// </para>
/// <para>
/// Every date is an <b>offset from <paramref name="now"/></b>, never a literal
/// timestamp, so the fixture still reads as current a year from now while
/// staying deterministic for any given <c>now</c>.
/// </para>
/// <para>
/// The dataset deliberately covers all seven categories, all five statuses, and
/// a set of edge cases documented in <c>docs/DEMO_DATA.md</c>.
/// </para>
/// </remarks>
public static class DemoData
{
    // Keys for the showcase chain: Desk unblocks Monitor unblocks Arm. The arm
    // is the highest-priority item in the whole set, so its priority propagates
    // back down to the desk and the desk is what the dashboard recommends first.
    private const string Desk = "desk";
    private const string Monitor = "monitor";
    private const string Arm = "arm";

    public static IReadOnlyList<DemoUpgrade> Build(DateTime now)
    {
        return
        [
            // --- The showcase chain -------------------------------------------------
            Row(Desk, null, new Upgrade
            {
                Title = "Bigger desk",
                Description = "The current one is 100cm wide. A second monitor does not fit next to the laptop stand.",
                Category = UpgradeCategory.Office,
                Priority = 70,
                EstimatedCost = 700m,
                Status = UpgradeStatus.ReadyToBuy,
                Notes = "Measured the alcove: anything up to 160cm will fit.",
                CreatedAt = now.AddDays(-46),
                UpdatedAt = now.AddDays(-9),
            }),
            Row(Monitor, Desk, new Upgrade
            {
                Title = "Third monitor",
                Description = "A third panel for keeping logs visible while debugging.",
                Category = UpgradeCategory.Technology,
                Priority = 75,
                EstimatedCost = 250m,
                Status = UpgradeStatus.Idea,
                CreatedAt = now.AddDays(-44),
                UpdatedAt = now.AddDays(-44),
            }),
            Row(Arm, Monitor, new Upgrade
            {
                Title = "Monitor arm",
                Description = "Frees the desk surface and fixes the neck angle. Pointless until the monitor exists.",
                Category = UpgradeCategory.Technology,
                Priority = 92,
                EstimatedCost = 60m,
                Status = UpgradeStatus.Idea,
                CreatedAt = now.AddDays(-44),
                UpdatedAt = now.AddDays(-30),
            }),

            // --- Unblocked items it has to out-rank ---------------------------------
            Row("chair", null, new Upgrade
            {
                Title = "Ergonomic office chair",
                Description = "Eight hours a day in a dining chair is catching up with me.",
                Category = UpgradeCategory.Office,
                Priority = 85,
                EstimatedCost = 800m,
                Status = UpgradeStatus.Researching,
                Notes = "Shortlist is down to two. Sit in both before deciding.",
                ProductLink = "https://example.com/catalog/ergonomic-chair",
                CreatedAt = now.AddDays(-61),
                UpdatedAt = now.AddDays(-4),
            }),
            Row("tyres", null, new Upgrade
            {
                Title = "Winter tyres",
                Description = "Seasonal, and the deadline is not negotiable.",
                Category = UpgradeCategory.Vehicle,
                Priority = 88,
                EstimatedCost = 640m,
                Status = UpgradeStatus.ReadyToBuy,
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-2),
            }),
            Row("nas", null, new Upgrade
            {
                Title = "Home NAS",
                Description = "Two drives, mirrored, so the photo library stops living on one laptop.",
                Category = UpgradeCategory.Technology,
                Priority = 60,
                EstimatedCost = 500m,
                Status = UpgradeStatus.Idea,
                CreatedAt = now.AddDays(-38),
                UpdatedAt = now.AddDays(-38),
            }),
            Row("curtains", null, new Upgrade
            {
                Title = "Blackout curtains",
                Category = UpgradeCategory.Home,
                Priority = 30,
                EstimatedCost = 180m,
                Status = UpgradeStatus.ReadyToBuy,
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-20),
            }),

            // --- Completed and abandoned, so those states are demonstrated ----------
            Row("bench", null, new Upgrade
            {
                Title = "Adjustable weight bench",
                Description = "Replaces the flat bench; needed for incline work.",
                Category = UpgradeCategory.Gym,
                Priority = 50,
                EstimatedCost = 300m,
                Status = UpgradeStatus.Purchased,
                ActualCost = 275m,
                PurchasedDate = now.AddDays(-18),
                Notes = "Came in under estimate — bought last season's model.",
                CreatedAt = now.AddDays(-52),
                UpdatedAt = now.AddDays(-18),
            }),
            Row("vacuum", null, new Upgrade
            {
                Title = "Robot vacuum",
                Category = UpgradeCategory.Home,
                Priority = 42,
                EstimatedCost = 260m,
                Status = UpgradeStatus.Purchased,
                ActualCost = 319m,
                PurchasedDate = now.AddDays(-73),
                Notes = "Went over estimate. The model that handles rugs costs more.",
                CreatedAt = now.AddDays(-96),
                UpdatedAt = now.AddDays(-73),
            }),
            Row("treadmill", null, new Upgrade
            {
                Title = "Under-desk treadmill",
                Description = "Walking pad to put under the standing desk.",
                Category = UpgradeCategory.Lifestyle,
                Priority = 40,
                EstimatedCost = 900m,
                Status = UpgradeStatus.Cancelled,
                Notes = "Cancelled: the floor upstairs is not level enough and the noise carries.",
                CreatedAt = now.AddDays(-80),
                UpdatedAt = now.AddDays(-35),
            }),

            // --- Deliberate edge cases ----------------------------------------------

            // Minimal record: only the required fields. Everything optional is null.
            Row("washer", null, new Upgrade
            {
                Title = "Replace the kitchen tap washer",
                Category = UpgradeCategory.Other,
                Priority = 1, // lower boundary of the CK_Upgrade_Priority constraint
                Status = UpgradeStatus.Idea,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-6),
            }),

            // Upper priority boundary, and a null cost — the cost column has to
            // render an em dash rather than "$0" or "NaN".
            Row("radiator", null, new Upgrade
            {
                Title = "Fix the bedroom radiator",
                Description = "It has not produced heat since spring. Cost unknown until someone looks at it.",
                Category = UpgradeCategory.Home,
                Priority = 100, // upper boundary of the CK_Upgrade_Priority constraint
                EstimatedCost = null,
                Status = UpgradeStatus.ReadyToBuy,
                CreatedAt = now.AddDays(-13),
                UpdatedAt = now.AddDays(-1),
            }),

            // Long title and long description, to prove the layout truncates or wraps
            // instead of overflowing.
            Row("shed", null, new Upgrade
            {
                Title = "Replace the garden shed roof felt before the autumn rain gets into the timber",
                Description =
                    "The felt has split along the ridge and two of the rafters are already damp to the touch. "
                    + "Doing it now costs a roll of felt and an afternoon. Leaving it until the timber rots "
                    + "means replacing rafters, which means taking the whole roof off, which means the "
                    + "contents have to go somewhere for a weekend. This description is deliberately long "
                    + "so the detail view has something substantial to lay out.",
                Category = UpgradeCategory.Other,
                Priority = 66,
                EstimatedCost = 95m,
                Status = UpgradeStatus.Researching,
                CreatedAt = now.AddDays(-11),
                UpdatedAt = now.AddDays(-11),
            }),
        ];
    }

    private static DemoUpgrade Row(string key, string? prerequisiteKey, Upgrade entity)
        => new(key, prerequisiteKey, entity);
}
