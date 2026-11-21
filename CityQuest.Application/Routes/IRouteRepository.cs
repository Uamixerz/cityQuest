using CityQuest.Domain.Entities;

namespace CityQuest.Application.Routes;

public interface IRouteRepository
{
    Task<List<Place>> GetCandidatesAsync(RoutePlanRequest request, CancellationToken cancellationToken);
    Task AddAsync(WalkingRoute route, CancellationToken cancellationToken);
    Task ReplaceAsync(WalkingRoute original, WalkingRoute updated, CancellationToken cancellationToken);
    Task<WalkingRoute?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<RouteResponse> GetDetailsAsync(WalkingRoute route, string? userId, CancellationToken cancellationToken);
    Task<RoutePage> GetListAsync(RouteQuery query, string? userId, string scope, CancellationToken cancellationToken);
    Task SetLikeAsync(Guid routeId, string userId, bool value, CancellationToken cancellationToken);
    Task SetSavedAsync(Guid routeId, string userId, bool value, CancellationToken cancellationToken);
    Task PublishAsync(Guid routeId, CancellationToken cancellationToken);
}
