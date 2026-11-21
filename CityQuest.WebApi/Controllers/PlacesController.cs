using CityQuest.Application.Places;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityQuest.WebApi.Controllers;

[ApiController]
[Route("api/places")]
public class PlacesController(PlaceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlaceResponse>>> GetAll([FromQuery] string? category,
        [FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(category, search, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlaceResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlaceResponse>> Create(PlaceRequest request, CancellationToken cancellationToken)
    {
        var place = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = place.Id }, place);
    }

    [HttpGet("{id:guid}/hint")]
    public async Task<IActionResult> GetHint(Guid id, CancellationToken cancellationToken) =>
        Ok(new { hint = await service.GetHintAsync(id, cancellationToken) });

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, PlaceRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
