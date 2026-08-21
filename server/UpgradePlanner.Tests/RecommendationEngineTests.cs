using UpgradePlanner.Api.Models;
using UpgradePlanner.Api.Services;

namespace UpgradePlanner.Tests;

/// <summary>
/// The core business logic: how priority moves through the prerequisite graph.
/// </summary>
/// <remarks>
/// This is the algorithm the whole product rests on, and it is a pure function,
/// so it is tested directly with no database and no HTTP.
/// </remarks>
public class RecommendationEngineTests
{
    private static Upgrade Item(
        int id,
        int priority,
        int? prerequisiteId = null,
        UpgradeStatus status = UpgradeStatus.Idea,
        string? title = null)
        => new()
        {
            Id = id,
            Title = title ?? $"Item {id}",
            Priority = priority,
            Status = status,
            PrerequisiteUpgradeId = prerequisiteId,
        };

    [Fact]
    public void Priority_propagates_up_a_prerequisite_chain()
    {
        // Desk (70) unblocks Monitor (75) unblocks Arm (92).
        // The desk should inherit 92, because buying it is the first step to the arm.
        var desk = Item(1, 70, title: "Desk");
        var monitor = Item(2, 75, prerequisiteId: 1, title: "Monitor");
        var arm = Item(3, 92, prerequisiteId: 2, title: "Arm");

        var result = RecommendationEngine.Compute([desk, monitor, arm]);

        Assert.Equal(92, result[desk.Id].EffectivePriority);
        Assert.Equal(92, result[monitor.Id].EffectivePriority);
        Assert.Equal(92, result[arm.Id].EffectivePriority);
    }

    [Fact]
    public void Effective_priority_names_the_descendant_it_came_from()
    {
        var desk = Item(1, 70, title: "Desk");
        var monitor = Item(2, 75, prerequisiteId: 1, title: "Monitor");
        var arm = Item(3, 92, prerequisiteId: 2, title: "Arm");

        var result = RecommendationEngine.Compute([desk, monitor, arm]);

        // Not the immediate child (Monitor) - the highest-priority descendant.
        Assert.Equal("Arm", result[desk.Id].UnlocksTitle);
        Assert.Equal(3, result[desk.Id].UnlocksUpgradeId);
    }

    [Fact]
    public void An_item_that_unlocks_nothing_reports_no_unlock_source()
    {
        var lonely = Item(1, 40);

        var result = RecommendationEngine.Compute([lonely]);

        Assert.Equal(40, result[1].EffectivePriority);
        Assert.Null(result[1].UnlocksUpgradeId);
        Assert.Null(result[1].UnlocksTitle);
    }

    [Fact]
    public void A_high_priority_parent_is_not_dragged_down_by_a_low_priority_child()
    {
        var parent = Item(1, 90);
        var child = Item(2, 10, prerequisiteId: 1);

        var result = RecommendationEngine.Compute([parent, child]);

        Assert.Equal(90, result[1].EffectivePriority);
        Assert.Null(result[1].UnlocksUpgradeId);
    }

    [Fact]
    public void An_item_is_blocked_while_its_prerequisite_is_unpurchased()
    {
        var desk = Item(1, 70, status: UpgradeStatus.ReadyToBuy);
        var monitor = Item(2, 75, prerequisiteId: 1);

        var result = RecommendationEngine.Compute([desk, monitor]);

        Assert.False(result[1].IsBlocked);
        Assert.True(result[2].IsBlocked);
    }

    [Fact]
    public void An_item_is_unblocked_once_its_prerequisite_is_purchased()
    {
        var desk = Item(1, 70, status: UpgradeStatus.Purchased);
        var monitor = Item(2, 75, prerequisiteId: 1);

        var result = RecommendationEngine.Compute([desk, monitor]);

        Assert.False(result[2].IsBlocked);
    }

    [Theory]
    [InlineData(UpgradeStatus.Idea)]
    [InlineData(UpgradeStatus.Researching)]
    [InlineData(UpgradeStatus.ReadyToBuy)]
    [InlineData(UpgradeStatus.Cancelled)]
    public void Only_a_purchased_prerequisite_unblocks(UpgradeStatus prerequisiteStatus)
    {
        var prerequisite = Item(1, 50, status: prerequisiteStatus);
        var dependent = Item(2, 60, prerequisiteId: 1);

        var result = RecommendationEngine.Compute([prerequisite, dependent]);

        Assert.True(result[2].IsBlocked);
    }

    [Fact]
    public void An_item_with_no_prerequisite_is_never_blocked()
    {
        var result = RecommendationEngine.Compute([Item(1, 50), Item(2, 90)]);

        Assert.False(result[1].IsBlocked);
        Assert.False(result[2].IsBlocked);
    }

    [Fact]
    public void A_dangling_prerequisite_reference_does_not_throw()
    {
        // The foreign key makes this unreachable through the API, but the engine
        // is a pure function that anything could hand a partial list to.
        var orphan = Item(1, 50, prerequisiteId: 999);

        var result = RecommendationEngine.Compute([orphan]);

        Assert.False(result[1].IsBlocked);
        Assert.Equal(50, result[1].EffectivePriority);
    }

    [Fact]
    public void A_cyclic_chain_terminates_instead_of_recursing_forever()
    {
        // Not reachable through the API - UpgradeService rejects cycles - but if a
        // row were ever corrupted, the engine must degrade rather than hang.
        var a = Item(1, 10, prerequisiteId: 2);
        var b = Item(2, 20, prerequisiteId: 1);

        var result = RecommendationEngine.Compute([a, b]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void An_empty_set_produces_an_empty_result()
    {
        Assert.Empty(RecommendationEngine.Compute([]));
    }

    [Fact]
    public void A_branch_takes_the_highest_priority_of_several_children()
    {
        var root = Item(1, 10);
        var low = Item(2, 30, prerequisiteId: 1);
        var high = Item(3, 80, prerequisiteId: 1, title: "Winner");

        var result = RecommendationEngine.Compute([root, low, high]);

        Assert.Equal(80, result[1].EffectivePriority);
        Assert.Equal("Winner", result[1].UnlocksTitle);
    }
}
