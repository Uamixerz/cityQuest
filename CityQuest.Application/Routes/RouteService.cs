using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CityQuest.Domain;
using CityQuest.Application.Exceptions;
using CityQuest.Domain.Entities;

namespace CityQuest.Application.Routes;

public class RouteService(IRouteRepository repository, IWalkingRouteService walkingRoutes)
{
    public async Task<RoutePlanResponse> PreviewAsync(RoutePlanRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        var places = await repository.GetCandidatesAsync(request, cancellationToken);
        return await walkingRoutes.PlanAsync(request, places, cancellationToken);
    }

    public async Task<RouteResponse> CreateAsync(CreateRouteRequest request, string userId,
        CancellationToken cancellationToken)
    {
        var route = await BuildAsync(request, userId, cancellationToken);
        await repository.AddAsync(route, cancellationToken);
        return RouteResponse.FromEntity(route, userId, 0, false, false);
    }

    public async Task<RouteResponse> UpdateAsync(Guid id, CreateRouteRequest request, string userId, CancellationToken cancellationToken)
    {
        var original = await repository.FindAsync(id, cancellationToken);
        if (original is null || original.OwnerId != userId)
            throw new NotFoundException("Маршрут не знайдено.");
        var route = await BuildAsync(request, userId, cancellationToken);
        route.Id = id;
        route.CreatedAt = original.CreatedAt;
        route.IsPublic = original.IsPublic;
        await repository.ReplaceAsync(original, route, cancellationToken);
        return await GetAsync(id, userId, cancellationToken);
    }

    private async Task<WalkingRoute> BuildAsync(CreateRouteRequest request, string userId, CancellationToken cancellationToken)
    {
        var plan = await PreviewAsync(request, cancellationToken);
        var interests = request.PlaceIds is null ? request.Interests : plan.Stops.Select(x => x.Category)
            .Where(PlaceCategories.Attractions.Contains).ToArray();
        return new WalkingRoute
        {
            OwnerId = userId, Name = request.Name.Trim(), IsPublic = request.IsPublic,
            StartLatitude = request.StartLatitude, StartLongitude = request.StartLongitude,
            ReturnToStart = request.ReturnToStart, TimeBudgetMinutes = request.TimeBudgetMinutes,
            Interests = interests.Distinct().ToArray(), IncludeFood = plan.Stops.Any(x => PlaceCategories.IsFood(x.Category)),
            FoodType = request.FoodType, TotalMinutes = plan.TotalMinutes,
            WalkingMinutes = plan.WalkingMinutes, DistanceMeters = plan.DistanceMeters,
            ReturnWalkingMinutes = plan.ReturnWalkingMinutes, ReturnDistanceMeters = plan.ReturnDistanceMeters,
            CalculationMethod = plan.CalculationMethod,
            GeometryJson = plan.Geometry is null ? null : JsonSerializer.Serialize(plan.Geometry),
            Attribution = plan.Attribution,
            RoadWarnings = plan.Warnings.Except(RoutePlanner.GetWarnings(request.Interests,
                plan.Stops.Select(x => x.Category), plan.IsEstimated)).ToArray(),
            Stops = plan.Stops.Select(x => new RouteStop
            {
                Position = x.Position, PlaceId = x.PlaceId, Name = x.Name, Category = x.Category,
                Latitude = x.Latitude, Longitude = x.Longitude, VisitMinutes = x.VisitMinutes,
                WalkingMinutes = x.WalkingMinutes, DistanceMeters = x.DistanceMeters
            }).ToList()
        };
    }

    public async Task<RouteResponse> GetAsync(Guid id, string? userId, CancellationToken cancellationToken)
    {
        var route = await FindVisibleAsync(id, userId, cancellationToken);
        return await repository.GetDetailsAsync(route, userId, cancellationToken);
    }

    public Task<RoutePage> ListAsync(RouteQuery query, string? userId, string scope,
        CancellationToken cancellationToken)
    {
        Validator.ValidateObject(query, new ValidationContext(query), true);
        return repository.GetListAsync(query, userId, scope, cancellationToken);
    }

    public async Task LikeAsync(Guid id, string userId, bool value, CancellationToken cancellationToken)
    {
        var route = await FindVisibleAsync(id, userId, cancellationToken);
        if (!route.IsPublic)
            throw new ConflictException("Лайкати можна лише публічні маршрути.");
        await repository.SetLikeAsync(id, userId, value, cancellationToken);
    }

    public async Task SaveAsync(Guid id, string userId, bool value, CancellationToken cancellationToken)
    {
        await FindVisibleAsync(id, userId, cancellationToken);
        await repository.SetSavedAsync(id, userId, value, cancellationToken);
    }

    public async Task PublishAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var route = await FindVisibleAsync(id, userId, cancellationToken);
        if (route.OwnerId != userId)
            throw new NotFoundException("Маршрут не знайдено.");
        await repository.PublishAsync(id, cancellationToken);
    }

    private async Task<WalkingRoute> FindVisibleAsync(Guid id, string? userId, CancellationToken cancellationToken)
    {
        var route = await repository.FindAsync(id, cancellationToken);
        if (route is null || (!route.IsPublic && route.OwnerId != userId))
            throw new NotFoundException("Маршрут не знайдено.");
        return route;
    }
}
