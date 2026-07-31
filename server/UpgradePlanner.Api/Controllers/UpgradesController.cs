using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Data;
using UpgradePlanner.Api.Dtos;
using UpgradePlanner.Api.Models;
using UpgradePlanner.Api.Services;

namespace UpgradePlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UpgradesController : ControllerBase
{
    private readonly AppDbContext _db;

    public UpgradesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<UpgradeDto>>> GetAll()
    {
        var upgrades = await _db.Upgrades.AsNoTracking().ToListAsync();
        var computed = RecommendationEngine.Compute(upgrades);
        return upgrades
            .Select(u => ToDto(u, computed[u.Id], upgrades))
            .OrderByDescending(d => d.Priority)
            .ToList();
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<List<UpgradeDto>>> GetRecommendations()
    {
        var upgrades = await _db.Upgrades.AsNoTracking().ToListAsync();
        var computed = RecommendationEngine.Compute(upgrades);
        return upgrades
            .Where(u => u.Status is not (UpgradeStatus.Purchased or UpgradeStatus.Cancelled))
            .Select(u => ToDto(u, computed[u.Id], upgrades))
            .OrderByDescending(d => d.EffectivePriority)
            .ThenByDescending(d => d.Priority)
            .ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UpgradeDto>> GetById(int id)
    {
        var all = await _db.Upgrades.AsNoTracking().ToListAsync();
        var upgrade = all.FirstOrDefault(u => u.Id == id);
        if (upgrade is null) return NotFound();

        var computed = RecommendationEngine.Compute(all);
        return ToDto(upgrade, computed[upgrade.Id], all);
    }

    [HttpPost]
    public async Task<ActionResult<UpgradeDto>> Create(UpsertUpgradeDto input)
    {
        if (input.PrerequisiteUpgradeId is not null
            && !await _db.Upgrades.AnyAsync(u => u.Id == input.PrerequisiteUpgradeId))
        {
            return BadRequest("Prerequisite upgrade does not exist.");
        }

        var upgrade = new Upgrade
        {
            Title = input.Title,
            Description = input.Description,
            Category = input.Category,
            Priority = input.Priority,
            EstimatedCost = input.EstimatedCost,
            Status = input.Status,
            Notes = input.Notes,
            ProductLink = input.ProductLink,
            PrerequisiteUpgradeId = input.PrerequisiteUpgradeId,
            PurchasedDate = input.PurchasedDate,
            ActualCost = input.ActualCost,
        };

        _db.Upgrades.Add(upgrade);
        await _db.SaveChangesAsync();

        var all = await _db.Upgrades.AsNoTracking().ToListAsync();
        var computed = RecommendationEngine.Compute(all);
        return CreatedAtAction(nameof(GetById), new { id = upgrade.Id }, ToDto(upgrade, computed[upgrade.Id], all));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UpgradeDto>> Update(int id, UpsertUpgradeDto input)
    {
        var upgrade = await _db.Upgrades.FirstOrDefaultAsync(u => u.Id == id);
        if (upgrade is null) return NotFound();

        if (input.PrerequisiteUpgradeId is not null)
        {
            if (!await _db.Upgrades.AnyAsync(u => u.Id == input.PrerequisiteUpgradeId))
                return BadRequest("Prerequisite upgrade does not exist.");

            if (await WouldCreateCycleAsync(id, input.PrerequisiteUpgradeId))
                return BadRequest("This prerequisite would create a dependency cycle.");
        }

        upgrade.Title = input.Title;
        upgrade.Description = input.Description;
        upgrade.Category = input.Category;
        upgrade.Priority = input.Priority;
        upgrade.EstimatedCost = input.EstimatedCost;
        upgrade.Status = input.Status;
        upgrade.Notes = input.Notes;
        upgrade.ProductLink = input.ProductLink;
        upgrade.PrerequisiteUpgradeId = input.PrerequisiteUpgradeId;
        upgrade.PurchasedDate = input.PurchasedDate;
        upgrade.ActualCost = input.ActualCost;

        await _db.SaveChangesAsync();

        var all = await _db.Upgrades.AsNoTracking().ToListAsync();
        var computed = RecommendationEngine.Compute(all);
        return ToDto(upgrade, computed[upgrade.Id], all);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var upgrade = await _db.Upgrades.FirstOrDefaultAsync(u => u.Id == id);
        if (upgrade is null) return NotFound();

        var hasDependents = await _db.Upgrades.AnyAsync(u => u.PrerequisiteUpgradeId == id);
        if (hasDependents)
            return BadRequest("Cannot delete an upgrade that other upgrades depend on. Unlink dependents first.");

        _db.Upgrades.Remove(upgrade);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> WouldCreateCycleAsync(int upgradeId, int? newPrerequisiteId)
    {
        if (newPrerequisiteId is null) return false;
        if (newPrerequisiteId == upgradeId) return true;

        var currentId = newPrerequisiteId;
        var visited = new HashSet<int>();
        while (currentId is not null)
        {
            if (currentId == upgradeId) return true;
            if (!visited.Add(currentId.Value)) break;

            currentId = await _db.Upgrades
                .Where(u => u.Id == currentId)
                .Select(u => u.PrerequisiteUpgradeId)
                .FirstOrDefaultAsync();
        }

        return false;
    }

    private static UpgradeDto ToDto(Upgrade u, RecommendationEngine.Result computed, List<Upgrade> all)
    {
        var prerequisiteTitle = u.PrerequisiteUpgradeId.HasValue
            ? all.FirstOrDefault(x => x.Id == u.PrerequisiteUpgradeId)?.Title
            : null;

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
