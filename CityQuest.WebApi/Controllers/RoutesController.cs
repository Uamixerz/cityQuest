using System.Security.Claims;
using CityQuest.Application.Routes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityQuest.WebApi.Controllers;

[ApiController]
[Route("api/routes")]
public class RoutesController(RouteService service) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("preview")]
    public async Task<ActionResult<RoutePlanResponse>> Preview(RoutePlanRequest request, CancellationToken cancellationToken) =>
        Ok(await service.PreviewAsync(request, cancellationToken));

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<RouteResponse>> Create(CreateRouteRequest request, CancellationToken cancellationToken)
    {
        var route = await service.CreateAsync(request, UserId!, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = route.Id }, route);
    }

    [HttpGet]
    public async Task<ActionResult<RoutePage>> GetAll([FromQuery] RouteQuery query, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(query, UserId, "public", cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<RouteResponse>> Update(Guid id, CreateRouteRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, UserId!, cancellationToken));

    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<RoutePage>> GetMine([FromQuery] RouteQuery query, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(query, UserId, "mine", cancellationToken));

    [HttpGet("saved")]
    [Authorize]
    public async Task<ActionResult<RoutePage>> GetSaved([FromQuery] RouteQuery query, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(query, UserId, "saved", cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RouteResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, UserId, cancellationToken));

    [HttpPut("{id:guid}/like")]
    [Authorize]
    public async Task<IActionResult> Like(Guid id, CancellationToken cancellationToken)
    {
        await service.LikeAsync(id, UserId!, true, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/publish")]
    [Authorize]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        await service.PublishAsync(id, UserId!, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/like")]
    [Authorize]
    public async Task<IActionResult> Unlike(Guid id, CancellationToken cancellationToken)
    {
        await service.LikeAsync(id, UserId!, false, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/saved")]
    [Authorize]
    public async Task<IActionResult> Save(Guid id, CancellationToken cancellationToken)
    {
        await service.SaveAsync(id, UserId!, true, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/saved")]
    [Authorize]
    public async Task<IActionResult> Unsave(Guid id, CancellationToken cancellationToken)
    {
        await service.SaveAsync(id, UserId!, false, cancellationToken);
        return NoContent();
    }
}
