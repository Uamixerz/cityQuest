using CityQuest.Domain.Entities;

namespace CityQuest.Application.Routes;

public interface IWalkingRouteService
{
    Task<RoutePlanResponse> PlanAsync(RoutePlanRequest request, IReadOnlyList<Place> places, CancellationToken cancellationToken);
}

public record WalkingLeg(int Minutes, int Meters);

public class WalkingMatrix
{
    public Dictionary<(Guid From, Guid To), WalkingLeg?> Legs { get; } = [];
    public WalkingLeg? Get(Guid from, Guid to) => Legs.GetValueOrDefault((from, to));
}
