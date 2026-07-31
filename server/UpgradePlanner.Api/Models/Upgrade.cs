using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UpgradePlanner.Api.Models;

public class Upgrade
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public UpgradeCategory Category { get; set; }

    [Range(1, 100)]
    public int Priority { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? EstimatedCost { get; set; }

    public UpgradeStatus Status { get; set; } = UpgradeStatus.Idea;

    public string? Notes { get; set; }

    public string? ProductLink { get; set; }

    public int? PrerequisiteUpgradeId { get; set; }
    public Upgrade? PrerequisiteUpgrade { get; set; }

    public ICollection<Upgrade> DependentUpgrades { get; set; } = new List<Upgrade>();

    public DateTime? PurchasedDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualCost { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
