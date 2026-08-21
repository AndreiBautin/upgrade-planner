using System.ComponentModel.DataAnnotations;
using UpgradePlanner.Api.Models;
using UpgradePlanner.Api.Validation;

namespace UpgradePlanner.Api.Dtos;

/// <summary>
/// The request body for creating or updating an upgrade — and the API's trust
/// boundary. Everything that arrives from outside arrives through this type.
/// </summary>
/// <remarks>
/// <para>
/// The DTO is deliberately narrower than <see cref="Upgrade"/>: it has no
/// <c>Id</c>, no <c>CreatedAt</c>/<c>UpdatedAt</c>, and none of the derived
/// recommendation fields. A caller cannot assign an id, forge a creation date,
/// or claim an effective priority, because there is nowhere in the shape to put
/// those values.
/// </para>
/// <para>
/// Length and range limits are not cosmetic. Before they existed a 2,000,000
/// character <c>Notes</c> value was accepted and stored, which on a host with an
/// ephemeral disk and no authentication is a disk-exhaustion vector.
/// </para>
/// </remarks>
public class UpsertUpgradeDto : IValidatableObject
{
    /// <summary>Upper bound for free-text fields, chosen to be far beyond real use
    /// but small enough that a million of them cannot fill a free-tier disk.</summary>
    public const int MaxDescriptionLength = 2_000;
    public const int MaxNotesLength = 4_000;
    public const int MaxProductLinkLength = 2_048;

    /// <summary>Above this, a "cost" is a typo or an attack, not a purchase.</summary>
    public const double MaxCost = 1_000_000d;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(MaxDescriptionLength)]
    public string? Description { get; set; }

    [EnumDefined]
    public UpgradeCategory Category { get; set; }

    [Range(1, 100)]
    public int Priority { get; set; }

    [Range(0d, MaxCost)]
    public decimal? EstimatedCost { get; set; }

    [EnumDefined]
    public UpgradeStatus Status { get; set; } = UpgradeStatus.Idea;

    [MaxLength(MaxNotesLength)]
    public string? Notes { get; set; }

    [MaxLength(MaxProductLinkLength), HttpUrl]
    public string? ProductLink { get; set; }

    public int? PrerequisiteUpgradeId { get; set; }

    public DateTime? PurchasedDate { get; set; }

    [Range(0d, MaxCost)]
    public decimal? ActualCost { get; set; }

    /// <summary>
    /// Cross-field rules that no single-property attribute can express.
    /// </summary>
    /// <remarks>
    /// Purchase details previously survived on records that were never
    /// purchased: <c>{"status": 0, "purchasedDate": "2020-01-01", "actualCost": 9}</c>
    /// returned <c>201</c>. That produced rows the UI cannot render coherently —
    /// an "Idea" with a purchase date — and totals that counted money never
    /// spent.
    /// </remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // No whitespace-only Title check here: [Required] already trims before
        // testing for emptiness, so "   " is rejected upstream. A second check
        // would be one that can never fail.

        if (Status != UpgradeStatus.Purchased)
        {
            if (PurchasedDate is not null)
            {
                yield return new ValidationResult(
                    "PurchasedDate can only be set when Status is Purchased.", [nameof(PurchasedDate)]);
            }

            if (ActualCost is not null)
            {
                yield return new ValidationResult(
                    "ActualCost can only be set when Status is Purchased.", [nameof(ActualCost)]);
            }
        }

        // A purchase date in the future is a data-entry mistake. A day of slack
        // absorbs clock skew between the browser's timezone and the server's UTC.
        if (PurchasedDate is { } purchased && purchased > DateTime.UtcNow.AddDays(1))
        {
            yield return new ValidationResult(
                "PurchasedDate cannot be in the future.", [nameof(PurchasedDate)]);
        }
    }
}
