using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Dtos;

public class UpgradeDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UpgradeCategory Category { get; set; }
    public int Priority { get; set; }
    public decimal? EstimatedCost { get; set; }
    public UpgradeStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? ProductLink { get; set; }
    public int? PrerequisiteUpgradeId { get; set; }
    public string? PrerequisiteTitle { get; set; }
    public DateTime? PurchasedDate { get; set; }
    public decimal? ActualCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Derived by RecommendationEngine
    public bool IsBlocked { get; set; }
    public int EffectivePriority { get; set; }
    public int? UnlocksUpgradeId { get; set; }
    public string? UnlocksTitle { get; set; }
}
