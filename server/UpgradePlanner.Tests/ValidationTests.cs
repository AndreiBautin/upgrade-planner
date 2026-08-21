using System.ComponentModel.DataAnnotations;
using UpgradePlanner.Api.Dtos;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Tests;

/// <summary>
/// Validation at the trust boundary — every one of these was accepted with a
/// <c>201 Created</c> before productionization, verified against a running
/// instance.
/// </summary>
/// <remarks>
/// These run the same <see cref="Validator"/> that <c>[ApiController]</c> runs
/// before an action method is entered, with <c>validateAllProperties</c> on,
/// so a pass here is a pass at the real boundary.
/// </remarks>
public class ValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(UpsertUpgradeDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static bool Rejects(UpsertUpgradeDto dto, string member)
        => Validate(dto).Any(r => r.MemberNames.Contains(member));

    private static UpsertUpgradeDto Valid() => new()
    {
        Title = "Bigger desk",
        Priority = 50,
        Category = UpgradeCategory.Office,
        Status = UpgradeStatus.Idea,
    };

    [Fact]
    public void A_well_formed_upgrade_is_accepted()
    {
        // Guards against over-strict rules: if this ever fails, the fixes below
        // have started rejecting legitimate input.
        Assert.Empty(Validate(Valid()));
    }

    [Fact]
    public void A_fully_populated_upgrade_is_accepted()
    {
        var dto = Valid();
        dto.Description = "A description.";
        dto.Notes = "Some notes.";
        dto.ProductLink = "https://example.com/catalog/desk";
        dto.EstimatedCost = 700m;
        dto.Status = UpgradeStatus.Purchased;
        dto.PurchasedDate = DateTime.UtcNow.AddDays(-3);
        dto.ActualCost = 650m;

        Assert.Empty(Validate(dto));
    }

    // --- Enum range ---------------------------------------------------------

    [Fact]
    public void A_category_outside_the_enum_is_rejected()
    {
        var dto = Valid();
        dto.Category = (UpgradeCategory)99;

        Assert.True(Rejects(dto, nameof(dto.Category)));
    }

    [Fact]
    public void A_status_outside_the_enum_is_rejected()
    {
        var dto = Valid();
        dto.Status = (UpgradeStatus)99;

        Assert.True(Rejects(dto, nameof(dto.Status)));
    }

    [Fact]
    public void A_negative_enum_value_is_rejected()
    {
        var dto = Valid();
        dto.Category = (UpgradeCategory)(-1);

        Assert.True(Rejects(dto, nameof(dto.Category)));
    }

    [Theory]
    [InlineData(UpgradeCategory.Home)]
    [InlineData(UpgradeCategory.Office)]
    [InlineData(UpgradeCategory.Gym)]
    [InlineData(UpgradeCategory.Technology)]
    [InlineData(UpgradeCategory.Vehicle)]
    [InlineData(UpgradeCategory.Lifestyle)]
    [InlineData(UpgradeCategory.Other)]
    public void Every_declared_category_is_accepted(UpgradeCategory category)
    {
        var dto = Valid();
        dto.Category = category;

        Assert.False(Rejects(dto, nameof(dto.Category)));
    }

    // --- Numeric range ------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public void A_priority_outside_one_to_a_hundred_is_rejected(int priority)
    {
        var dto = Valid();
        dto.Priority = priority;

        Assert.True(Rejects(dto, nameof(dto.Priority)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void The_priority_boundaries_themselves_are_accepted(int priority)
    {
        var dto = Valid();
        dto.Priority = priority;

        Assert.False(Rejects(dto, nameof(dto.Priority)));
    }

    [Fact]
    public void A_negative_estimated_cost_is_rejected()
    {
        var dto = Valid();
        dto.EstimatedCost = -500m;

        Assert.True(Rejects(dto, nameof(dto.EstimatedCost)));
    }

    [Fact]
    public void A_negative_actual_cost_is_rejected()
    {
        var dto = Valid();
        dto.Status = UpgradeStatus.Purchased;
        dto.PurchasedDate = DateTime.UtcNow.AddDays(-1);
        dto.ActualCost = -1m;

        Assert.True(Rejects(dto, nameof(dto.ActualCost)));
    }

    [Fact]
    public void A_zero_cost_is_accepted_because_free_things_exist()
    {
        var dto = Valid();
        dto.EstimatedCost = 0m;

        Assert.False(Rejects(dto, nameof(dto.EstimatedCost)));
    }

    // --- Length -------------------------------------------------------------

    [Fact]
    public void An_oversized_notes_field_is_rejected()
    {
        // 2,000,000 characters was accepted and stored before this limit existed.
        var dto = Valid();
        dto.Notes = new string('a', UpsertUpgradeDto.MaxNotesLength + 1);

        Assert.True(Rejects(dto, nameof(dto.Notes)));
    }

    [Fact]
    public void An_oversized_description_is_rejected()
    {
        var dto = Valid();
        dto.Description = new string('a', UpsertUpgradeDto.MaxDescriptionLength + 1);

        Assert.True(Rejects(dto, nameof(dto.Description)));
    }

    [Fact]
    public void A_notes_field_exactly_at_the_limit_is_accepted()
    {
        var dto = Valid();
        dto.Notes = new string('a', UpsertUpgradeDto.MaxNotesLength);

        Assert.False(Rejects(dto, nameof(dto.Notes)));
    }

    [Fact]
    public void An_oversized_title_is_rejected()
    {
        var dto = Valid();
        dto.Title = new string('a', 201);

        Assert.True(Rejects(dto, nameof(dto.Title)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_or_whitespace_title_is_rejected(string title)
    {
        var dto = Valid();
        dto.Title = title;

        Assert.True(Rejects(dto, nameof(dto.Title)));
    }

    // --- URL shape ----------------------------------------------------------

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url at all")]
    [InlineData("/relative/path")]
    public void A_product_link_that_is_not_an_http_url_is_rejected(string link)
    {
        var dto = Valid();
        dto.ProductLink = link;

        Assert.True(Rejects(dto, nameof(dto.ProductLink)));
    }

    [Theory]
    [InlineData("https://example.com/item")]
    [InlineData("http://example.com/item?variant=2")]
    public void A_real_http_url_is_accepted(string link)
    {
        var dto = Valid();
        dto.ProductLink = link;

        Assert.False(Rejects(dto, nameof(dto.ProductLink)));
    }

    // --- Cross-field coherence ----------------------------------------------

    [Fact]
    public void A_purchase_date_on_an_unpurchased_upgrade_is_rejected()
    {
        var dto = Valid();
        dto.Status = UpgradeStatus.Idea;
        dto.PurchasedDate = DateTime.UtcNow.AddDays(-10);

        Assert.True(Rejects(dto, nameof(dto.PurchasedDate)));
    }

    [Fact]
    public void An_actual_cost_on_an_unpurchased_upgrade_is_rejected()
    {
        var dto = Valid();
        dto.Status = UpgradeStatus.Researching;
        dto.ActualCost = 99m;

        Assert.True(Rejects(dto, nameof(dto.ActualCost)));
    }

    [Fact]
    public void A_purchase_date_in_the_future_is_rejected()
    {
        var dto = Valid();
        dto.Status = UpgradeStatus.Purchased;
        dto.PurchasedDate = DateTime.UtcNow.AddDays(30);

        Assert.True(Rejects(dto, nameof(dto.PurchasedDate)));
    }

    [Fact]
    public void A_purchased_upgrade_may_of_course_carry_purchase_details()
    {
        var dto = Valid();
        dto.Status = UpgradeStatus.Purchased;
        dto.PurchasedDate = DateTime.UtcNow.AddDays(-1);
        dto.ActualCost = 275m;

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void A_purchased_upgrade_with_no_recorded_details_is_still_valid()
    {
        // Marking something bought without remembering the price is legitimate.
        var dto = Valid();
        dto.Status = UpgradeStatus.Purchased;

        Assert.Empty(Validate(dto));
    }
}
