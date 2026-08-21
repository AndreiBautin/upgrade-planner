using UpgradePlanner.Api.Dtos;
using UpgradePlanner.Api.Models;
using UpgradePlanner.Api.Services;

namespace UpgradePlanner.Tests;

/// <summary>
/// The rules that used to be trapped inside the controller: what may reference
/// what, and what may be destroyed.
/// </summary>
/// <remarks>
/// The delete tests are written from the <b>"must not destroy"</b> side. The
/// interesting assertion is not that a delete works — it is that a delete which
/// would strand a dependent row is refused, and that the row is still there
/// afterwards.
/// </remarks>
public class UpgradeServiceTests : IDisposable
{
    private readonly TestDatabase _fixture = new();
    private readonly UpgradeService _service;

    public UpgradeServiceTests() => _service = new UpgradeService(_fixture.Db);

    public void Dispose() => _fixture.Dispose();

    private static UpsertUpgradeDto Input(
        string title = "Thing",
        int priority = 50,
        int? prerequisiteId = null,
        UpgradeStatus status = UpgradeStatus.Idea)
        => new()
        {
            Title = title,
            Priority = priority,
            PrerequisiteUpgradeId = prerequisiteId,
            Status = status,
        };

    private async Task<int> Given(string title, int priority = 50, int? prerequisiteId = null,
        UpgradeStatus status = UpgradeStatus.Idea)
    {
        var created = await _service.CreateAsync(Input(title, priority, prerequisiteId, status));
        Assert.Equal(ServiceStatus.Ok, created.Status);
        return created.Value!.Id;
    }

    // --- Deletion, from the side that must not happen ------------------------

    [Fact]
    public async Task Delete_is_refused_while_another_upgrade_depends_on_it()
    {
        var deskId = await Given("Desk");
        await Given("Monitor", prerequisiteId: deskId);

        var result = await _service.DeleteAsync(deskId);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains("depend", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_refused_delete_leaves_the_row_intact()
    {
        var deskId = await Given("Desk");
        await Given("Monitor", prerequisiteId: deskId);

        await _service.DeleteAsync(deskId);

        var stillThere = await _service.GetByIdAsync(deskId);
        Assert.Equal(ServiceStatus.Ok, stillThere.Status);
        Assert.Equal(2, (await _service.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Delete_succeeds_once_the_dependent_is_unlinked()
    {
        var deskId = await Given("Desk");
        var monitorId = await Given("Monitor", prerequisiteId: deskId);

        await _service.UpdateAsync(monitorId, Input("Monitor", prerequisiteId: null));
        var result = await _service.DeleteAsync(deskId);

        Assert.Equal(ServiceStatus.Ok, result.Status);
        Assert.Equal(ServiceStatus.NotFound, (await _service.GetByIdAsync(deskId)).Status);
    }

    [Fact]
    public async Task Deleting_something_that_does_not_exist_is_a_not_found_not_a_crash()
    {
        Assert.Equal(ServiceStatus.NotFound, (await _service.DeleteAsync(4242)).Status);
    }

    // --- Prerequisite integrity ---------------------------------------------

    [Fact]
    public async Task A_prerequisite_that_would_close_a_loop_is_rejected()
    {
        var aId = await Given("A");
        var bId = await Given("B", prerequisiteId: aId);

        // A already leads to B; pointing A at B would close the loop.
        var result = await _service.UpdateAsync(aId, Input("A", prerequisiteId: bId));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains("cycle", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_longer_loop_is_rejected_too()
    {
        var aId = await Given("A");
        var bId = await Given("B", prerequisiteId: aId);
        var cId = await Given("C", prerequisiteId: bId);

        var result = await _service.UpdateAsync(aId, Input("A", prerequisiteId: cId));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task An_upgrade_cannot_be_its_own_prerequisite()
    {
        var id = await Given("Self");

        var result = await _service.UpdateAsync(id, Input("Self", prerequisiteId: id));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task A_prerequisite_that_does_not_exist_is_rejected_on_create()
    {
        var result = await _service.CreateAsync(Input("Orphan", prerequisiteId: 999));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains("does not exist", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_prerequisite_that_does_not_exist_is_rejected_on_update()
    {
        var id = await Given("Thing");

        var result = await _service.UpdateAsync(id, Input("Thing", prerequisiteId: 999));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task A_legitimate_prerequisite_change_still_works()
    {
        var aId = await Given("A");
        var bId = await Given("B");
        var cId = await Given("C");

        var result = await _service.UpdateAsync(cId, Input("C", prerequisiteId: bId));

        Assert.Equal(ServiceStatus.Ok, result.Status);
        Assert.Equal(bId, result.Value!.PrerequisiteUpgradeId);
        Assert.Equal("B", result.Value.PrerequisiteTitle);
        Assert.NotEqual(aId, result.Value.PrerequisiteUpgradeId);
    }

    // --- Recommendations, end to end through the database --------------------

    [Fact]
    public async Task Recommendations_exclude_purchased_and_cancelled()
    {
        await Given("Open", status: UpgradeStatus.Idea);
        await Given("Bought", status: UpgradeStatus.Purchased);
        await Given("Dropped", status: UpgradeStatus.Cancelled);

        var titles = (await _service.GetRecommendationsAsync()).Select(u => u.Title).ToList();

        Assert.Equal(["Open"], titles);
    }

    [Fact]
    public async Task Recommendations_rank_an_unlocking_item_above_a_higher_priority_standalone()
    {
        // The product claim, verified end to end: the desk is only priority 70,
        // but it is the first step toward the priority-92 arm, so it outranks the
        // priority-85 chair.
        var deskId = await Given("Desk", priority: 70);
        var monitorId = await Given("Monitor", priority: 75, prerequisiteId: deskId);
        await Given("Arm", priority: 92, prerequisiteId: monitorId);
        await Given("Chair", priority: 85);

        var ranked = await _service.GetRecommendationsAsync();

        Assert.Equal("Arm", ranked[0].Title);      // 92, but blocked
        Assert.Contains("Desk", ranked.Take(3).Select(u => u.Title));
        Assert.True(
            ranked.FindIndex(u => u.Title == "Desk") < ranked.FindIndex(u => u.Title == "Chair"),
            "The desk should outrank the higher-priority chair because it unblocks the arm.");
    }

    [Fact]
    public async Task GetAll_orders_by_raw_priority_not_effective_priority()
    {
        var deskId = await Given("Desk", priority: 70);
        await Given("Arm", priority: 92, prerequisiteId: deskId);
        await Given("Chair", priority: 85);

        var titles = (await _service.GetAllAsync()).Select(u => u.Title).ToList();

        Assert.Equal(["Arm", "Chair", "Desk"], titles);
    }

    // --- Persistence behaviour ----------------------------------------------

    [Fact]
    public async Task Create_stamps_timestamps_the_caller_cannot_supply()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var created = await _service.CreateAsync(Input("Thing"));

        Assert.True(created.Value!.CreatedAt >= before);
        Assert.True(created.Value.UpdatedAt >= before);
    }

    [Fact]
    public async Task Update_moves_UpdatedAt_but_leaves_CreatedAt_alone()
    {
        var id = await Given("Thing");
        var original = (await _service.GetByIdAsync(id)).Value!;

        await Task.Delay(10);
        var updated = (await _service.UpdateAsync(id, Input("Thing renamed"))).Value!;

        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > original.UpdatedAt);
    }

    [Fact]
    public async Task Titles_are_trimmed_on_the_way_in()
    {
        var created = await _service.CreateAsync(Input("  Padded  "));

        Assert.Equal("Padded", created.Value!.Title);
    }

    [Fact]
    public async Task Getting_something_that_does_not_exist_is_a_not_found()
    {
        Assert.Equal(ServiceStatus.NotFound, (await _service.GetByIdAsync(4242)).Status);
    }

    [Fact]
    public async Task Updating_something_that_does_not_exist_is_a_not_found()
    {
        Assert.Equal(ServiceStatus.NotFound, (await _service.UpdateAsync(4242, Input())).Status);
    }
}
