using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Data;
using UpgradePlanner.Api.Dtos;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Services;

/// <summary>
/// Every business rule about upgrades: what may reference what, what may be
/// deleted, and how a stored row becomes the DTO the client sees.
/// </summary>
/// <remarks>
/// <para>
/// These rules used to live inside <c>UpgradesController</c>, where the only way
/// to reach them was an HTTP request. Moving them here is the one structural
/// change made during productionization, and it was made for a single reason:
/// <b>it is what allows them to be tested.</b> The cycle detector and the
/// delete-guard are now called directly by the test suite.
/// </para>
/// <para>
/// There is deliberately <b>no repository interface</b> between this class and
/// <see cref="AppDbContext"/>. <c>DbContext</c> already is a unit of work over a
/// set of repositories; wrapping it in another one would add a layer that
/// forwards calls and hides EF's query composition, in exchange for a test
/// double that the SQLite in-memory provider makes unnecessary.
/// </para>
/// </remarks>
public sealed class UpgradeService
{
    private readonly AppDbContext _db;

    public UpgradeService(AppDbContext db) => _db = db;

    /// <summary>All upgrades, highest raw priority first.</summary>
    public async Task<List<UpgradeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return Project(all)
            .OrderByDescending(d => d.Priority)
            .ToList();
    }

    /// <summary>
    /// What to buy next: everything still actionable, ordered by the priority it
    /// inherits from whatever it unblocks.
    /// </summary>
    /// <remarks>
    /// Purchased and Cancelled items are excluded because neither is something
    /// money can still be spent on. Blocked items stay in the list — seeing
    /// <i>why</i> a high-priority item is unavailable is the point of the view.
    /// </remarks>
    public async Task<List<UpgradeDto>> GetRecommendationsAsync(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return Project(all)
            .Where(d => d.Status is not (UpgradeStatus.Purchased or UpgradeStatus.Cancelled))
            .OrderByDescending(d => d.EffectivePriority)
            .ThenByDescending(d => d.Priority)
            .ToList();
    }

    /// <summary>
    /// One upgrade, including its derived fields.
    /// </summary>
    /// <remarks>
    /// This loads every row rather than one. That is not an oversight: effective
    /// priority is a property of the whole dependency graph, so the derived
    /// fields on a single upgrade cannot be computed without its descendants.
    /// At this scale (tens of rows) it is a single small query; see
    /// <c>docs/ARCHITECTURE.md</c> for where that stops being true.
    /// </remarks>
    public async Task<ServiceResult<UpgradeDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        var dto = Project(all).FirstOrDefault(d => d.Id == id);

        return dto is null ? ServiceResult<UpgradeDto>.NotFound() : ServiceResult<UpgradeDto>.Ok(dto);
    }

    public async Task<ServiceResult<UpgradeDto>> CreateAsync(UpsertUpgradeDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.PrerequisiteUpgradeId is { } prerequisiteId
            && !await _db.Upgrades.AnyAsync(u => u.Id == prerequisiteId, ct))
        {
            return ServiceResult<UpgradeDto>.Invalid("Prerequisite upgrade does not exist.");
        }

        // A brand-new row has no dependents yet, so it cannot close a cycle — the
        // cycle check below is only needed on update.
        var upgrade = new Upgrade();
        Apply(input, upgrade);

        _db.Upgrades.Add(upgrade);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(upgrade.Id, ct);
    }

    public async Task<ServiceResult<UpgradeDto>> UpdateAsync(int id, UpsertUpgradeDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var upgrade = await _db.Upgrades.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (upgrade is null) return ServiceResult<UpgradeDto>.NotFound();

        if (input.PrerequisiteUpgradeId is { } prerequisiteId)
        {
            if (!await _db.Upgrades.AnyAsync(u => u.Id == prerequisiteId, ct))
            {
                return ServiceResult<UpgradeDto>.Invalid("Prerequisite upgrade does not exist.");
            }

            if (await WouldCreateCycleAsync(id, prerequisiteId, ct))
            {
                return ServiceResult<UpgradeDto>.Invalid("This prerequisite would create a dependency cycle.");
            }
        }

        Apply(input, upgrade);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    /// <summary>
    /// Deletes an upgrade, refusing if anything still depends on it.
    /// </summary>
    /// <remarks>
    /// The database enforces this too, via <c>DeleteBehavior.Restrict</c> on the
    /// self-referencing foreign key. This check exists to turn a constraint
    /// violation into a sentence a person can act on, not to be the only thing
    /// standing between a dependent row and a dangling reference.
    /// </remarks>
    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var upgrade = await _db.Upgrades.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (upgrade is null) return ServiceResult<bool>.NotFound();

        if (await _db.Upgrades.AnyAsync(u => u.PrerequisiteUpgradeId == id, ct))
        {
            return ServiceResult<bool>.Invalid(
                "Cannot delete an upgrade that other upgrades depend on. Unlink dependents first.");
        }

        _db.Upgrades.Remove(upgrade);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Would pointing <paramref name="upgradeId"/> at <paramref name="newPrerequisiteId"/>
    /// close a loop?
    /// </summary>
    /// <remarks>
    /// Walks the proposed prerequisite's own ancestry looking for the upgrade
    /// being edited. The <c>visited</c> set is not for this walk's benefit — it
    /// stops an <i>already</i> corrupt chain from spinning forever, so a bad row
    /// degrades to "no cycle found" instead of hanging the request.
    /// </remarks>
    public async Task<bool> WouldCreateCycleAsync(int upgradeId, int? newPrerequisiteId, CancellationToken ct = default)
    {
        if (newPrerequisiteId is null) return false;

        var currentId = newPrerequisiteId;
        var visited = new HashSet<int>();

        while (currentId is not null)
        {
            if (currentId == upgradeId) return true;
            if (!visited.Add(currentId.Value)) break;

            currentId = await _db.Upgrades
                .Where(u => u.Id == currentId)
                .Select(u => u.PrerequisiteUpgradeId)
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }

    private Task<List<Upgrade>> LoadAllAsync(CancellationToken ct)
        => _db.Upgrades.AsNoTracking().ToListAsync(ct);

    /// <summary>Runs the recommendation engine once and maps every row through it.</summary>
    private static IEnumerable<UpgradeDto> Project(List<Upgrade> all)
    {
        var computed = RecommendationEngine.Compute(all);
        var titlesById = all.ToDictionary(u => u.Id, u => u.Title);

        return all.Select(u => ToDto(u, computed[u.Id], titlesById));
    }

    /// <summary>Copies the caller-settable fields, and only those.</summary>
    /// <remarks>
    /// <c>Id</c>, <c>CreatedAt</c> and <c>UpdatedAt</c> are absent by design:
    /// identity belongs to the database and timestamps belong to
    /// <see cref="AppDbContext.SaveChanges"/>.
    /// </remarks>
    private static void Apply(UpsertUpgradeDto input, Upgrade target)
    {
        target.Title = input.Title.Trim();
        target.Description = input.Description;
        target.Category = input.Category;
        target.Priority = input.Priority;
        target.EstimatedCost = input.EstimatedCost;
        target.Status = input.Status;
        target.Notes = input.Notes;
        target.ProductLink = input.ProductLink;
        target.PrerequisiteUpgradeId = input.PrerequisiteUpgradeId;
        target.PurchasedDate = input.PurchasedDate;
        target.ActualCost = input.ActualCost;
    }

    private static UpgradeDto ToDto(Upgrade u, RecommendationEngine.Result computed, Dictionary<int, string> titlesById)
    {
        string? prerequisiteTitle = null;
        if (u.PrerequisiteUpgradeId is { } prerequisiteId)
        {
            titlesById.TryGetValue(prerequisiteId, out prerequisiteTitle);
        }

        return new UpgradeDto
        {
            Id = u.Id,
            Title = u.Title,
            Description = u.Description,
            Category = u.Category,
            Priority = u.Priority,
            EstimatedCost = u.EstimatedCost,
            Status = u.Status,
            Notes = u.Notes,
            ProductLink = u.ProductLink,
            PrerequisiteUpgradeId = u.PrerequisiteUpgradeId,
            PrerequisiteTitle = prerequisiteTitle,
            PurchasedDate = u.PurchasedDate,
            ActualCost = u.ActualCost,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            IsBlocked = computed.IsBlocked,
            EffectivePriority = computed.EffectivePriority,
            UnlocksUpgradeId = computed.UnlocksUpgradeId,
            UnlocksTitle = computed.UnlocksTitle,
        };
    }
}
