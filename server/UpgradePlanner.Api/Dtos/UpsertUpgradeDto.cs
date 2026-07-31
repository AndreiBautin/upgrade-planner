using System.ComponentModel.DataAnnotations;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Dtos;

public class UpsertUpgradeDto
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public UpgradeCategory Category { get; set; }

    [Range(1, 100)]
    public int Priority { get; set; }

    public decimal? EstimatedCost { get; set; }

    public UpgradeStatus Status { get; set; } = UpgradeStatus.Idea;

    public string? Notes { get; set; }

    public string? ProductLink { get; set; }

    public int? PrerequisiteUpgradeId { get; set; }

    public DateTime? PurchasedDate { get; set; }

    public decimal? ActualCost { get; set; }
}
