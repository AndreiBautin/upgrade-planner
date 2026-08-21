using Microsoft.AspNetCore.Mvc;
using UpgradePlanner.Api.Dtos;
using UpgradePlanner.Api.Services;

namespace UpgradePlanner.Api.Controllers;

/// <summary>
/// HTTP surface for upgrades. Routing, status codes, and nothing else.
/// </summary>
/// <remarks>
/// Every business rule lives in <see cref="UpgradeService"/>. This class exists
/// to translate between HTTP and the domain: it never queries the database, and
/// it never decides whether something is allowed. Model validation happens
/// before any action runs, courtesy of <c>[ApiController]</c> plus the
/// annotations on <see cref="UpsertUpgradeDto"/>.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UpgradesController : ControllerBase
{
    private readonly UpgradeService _upgrades;

    public UpgradesController(UpgradeService upgrades) => _upgrades = upgrades;

    /// <summary>Every upgrade, highest priority first.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UpgradeDto>>> GetAll(CancellationToken ct)
        => await _upgrades.GetAllAsync(ct);

    /// <summary>What to spend money on next, ranked by inherited priority.</summary>
    [HttpGet("recommendations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UpgradeDto>>> GetRecommendations(CancellationToken ct)
        => await _upgrades.GetRecommendationsAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpgradeDto>> GetById(int id, CancellationToken ct)
        => Respond(await _upgrades.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UpgradeDto>> Create(UpsertUpgradeDto input, CancellationToken ct)
    {
        var result = await _upgrades.CreateAsync(input, ct);

        return result.Status == ServiceStatus.Ok
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : Respond(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpgradeDto>> Update(int id, UpsertUpgradeDto input, CancellationToken ct)
        => Respond(await _upgrades.UpdateAsync(id, input, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _upgrades.DeleteAsync(id, ct);

        return result.Status switch
        {
            ServiceStatus.Ok => NoContent(),
            ServiceStatus.NotFound => NotFound(),
            _ => BadRequest(result.Message),
        };
    }

    /// <summary>The single place domain outcomes become status codes.</summary>
    private ActionResult<T> Respond<T>(ServiceResult<T> result) => result.Status switch
    {
        ServiceStatus.Ok => result.Value!,
        ServiceStatus.NotFound => NotFound(),
        _ => BadRequest(result.Message),
    };
}
