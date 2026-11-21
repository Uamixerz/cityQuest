using CityQuest.Domain.Entities;
using System.Text.Json;

namespace CityQuest.Application.Routes;

public record RouteStopResponse(int Position, Guid PlaceId, string Name, string Category,
    double Latitude, double Longitude, int VisitMinutes, int WalkingMinutes, int DistanceMeters);

public record RoutePlanResponse(double StartLatitude, double StartLongitude, bool ReturnToStart,
    int TimeBudgetMinutes, int TotalMinutes, int WalkingMinutes, int DistanceMeters,
    int ReturnWalkingMinutes, int ReturnDistanceMeters, string CalculationMethod,
    bool IsEstimated, IReadOnlyList<string> Warnings, IReadOnlyList<RouteStopResponse> Stops,
    RouteGeometry? Geometry = null, string? Attribution = null);

public record RouteGeometry(string Type, double[][] Coordinates);

public record RouteResponse(Guid Id, string Name, bool IsPublic, bool IsOwner, DateTime CreatedAt,
    string[] Interests, bool IncludeFood, string? FoodType, int LikeCount, bool IsLiked, bool IsSaved,
    RoutePlanResponse Plan)
{
    public static RouteResponse FromEntity(WalkingRoute route, string? userId, int likes, bool liked, bool saved) =>
        new(route.Id, route.Name, route.IsPublic, route.OwnerId == userId, route.CreatedAt,
            route.Interests, route.IncludeFood, route.FoodType, likes, liked, saved,
            new RoutePlanResponse(route.StartLatitude, route.StartLongitude, route.ReturnToStart,
                route.TimeBudgetMinutes, route.TotalMinutes, route.WalkingMinutes, route.DistanceMeters,
                route.ReturnWalkingMinutes, route.ReturnDistanceMeters, route.CalculationMethod,
                route.CalculationMethod == "estimated-walk-v1",
                RoutePlanner.GetWarnings(route.Interests, route.Stops.Select(x => x.Category),
                    route.CalculationMethod == "estimated-walk-v1").Concat(route.RoadWarnings).ToArray(),
                route.Stops.OrderBy(x => x.Position).Select(x => new RouteStopResponse(x.Position,
                    x.PlaceId, x.Name, x.Category, x.Latitude, x.Longitude, x.VisitMinutes,
                    x.WalkingMinutes, x.DistanceMeters)).ToList(),
                route.GeometryJson is null ? null : JsonSerializer.Deserialize<RouteGeometry>(route.GeometryJson),
                route.Attribution));
}

public record RouteSummary(Guid Id, string Name, DateTime CreatedAt, int TotalMinutes,
    int DistanceMeters, int StopCount, bool IncludeFood, int LikeCount, bool IsLiked, bool IsSaved,
    bool IsEstimated = true);

public record RoutePage(IReadOnlyList<RouteSummary> Items, int TotalCount, int Page, int PageSize);
