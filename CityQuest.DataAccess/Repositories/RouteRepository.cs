using CityQuest.Application.Exceptions;
using CityQuest.Application.Routes;
using CityQuest.Domain;
using CityQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CityQuest.DataAccess.Repositories;

public class RouteRepository(CityQuestDbContext context) : IRouteRepository
{
    public Task<List<Place>> GetCandidatesAsync(RoutePlanRequest request, CancellationToken cancellationToken)
    {
        if (request.PlaceIds is { } ids)
            return context.Places.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        var categories = request.Interests.Concat(request.IncludeFood
            ? request.FoodType is null ? PlaceCategories.Food : [request.FoodType]
            : []).ToArray();
        return context.Places.AsNoTracking().Where(x => categories.Contains(x.Category)
            && x.Latitude >= request.StartLatitude - 0.23 && x.Latitude <= request.StartLatitude + 0.23)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WalkingRoute route, CancellationToken cancellationToken)
    {
        context.Routes.Add(route);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_RouteStops_Places_PlaceId" })
        {
            throw new ConflictException("Одна з локацій була видалена. Побудуйте маршрут повторно.");
        }
    }

    public Task<WalkingRoute?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Routes.AsNoTracking().Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task ReplaceAsync(WalkingRoute original, WalkingRoute updated, CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        context.Attach(original);
        context.Entry(original).CurrentValues.SetValues(updated);
        context.RouteStops.RemoveRange(original.Stops);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var stop in updated.Stops)
            stop.RouteId = original.Id;
        context.RouteStops.AddRange(updated.Stops);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RouteResponse> GetDetailsAsync(WalkingRoute route, string? userId, CancellationToken cancellationToken)
    {
        var count = await context.RouteLikes.CountAsync(x => x.RouteId == route.Id, cancellationToken);
        var liked = userId is not null && await context.RouteLikes.AnyAsync(x =>
            x.RouteId == route.Id && x.UserId == userId, cancellationToken);
        var saved = userId is not null && await context.SavedRoutes.AnyAsync(x =>
            x.RouteId == route.Id && x.UserId == userId, cancellationToken);
        return RouteResponse.FromEntity(route, userId, count, liked, saved);
    }

    public async Task<RoutePage> GetListAsync(RouteQuery query, string? userId, string scope, CancellationToken cancellationToken)
    {
        var routes = context.Routes.AsNoTracking();
        routes = scope switch
        {
            "mine" => routes.Where(x => x.OwnerId == userId),
            "saved" => routes.Where(x => (x.IsPublic || x.OwnerId == userId) && x.Saves.Any(s => s.UserId == userId)),
            _ => routes.Where(x => x.IsPublic)
        };
        if (query.Category is not null)
            routes = routes.Where(x => x.Stops.Any(s => s.Category == query.Category));
        if (query.MaxMinutes.HasValue)
            routes = routes.Where(x => x.TotalMinutes <= query.MaxMinutes.Value);
        if (query.IncludeFood.HasValue)
            routes = routes.Where(x => x.IncludeFood == query.IncludeFood.Value);

        var count = await routes.CountAsync(cancellationToken);
        var ordered = query.Sort == "newest"
            ? routes.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            : routes.OrderByDescending(x => x.Likes.Count).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id);
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new RouteSummary(x.Id, x.Name, x.CreatedAt, x.TotalMinutes, x.DistanceMeters,
                x.Stops.Count, x.IncludeFood, x.Likes.Count,
                userId != null && x.Likes.Any(l => l.UserId == userId),
                userId != null && x.Saves.Any(s => s.UserId == userId),
                x.CalculationMethod == "estimated-walk-v1"))
            .ToListAsync(cancellationToken);
        return new RoutePage(items, count, query.Page, query.PageSize);
    }

    public async Task SetLikeAsync(Guid routeId, string userId, bool value, CancellationToken cancellationToken)
    {
        var like = await context.RouteLikes.FindAsync([routeId, userId], cancellationToken);
        if (value && like is null)
        {
            like = new RouteLike { RouteId = routeId, UserId = userId };
            context.RouteLikes.Add(like);
        }
        else if (!value && like is not null)
            context.RouteLikes.Remove(like);
        else
            return;
        await SaveMembershipAsync(like, value, cancellationToken);
    }

    public async Task PublishAsync(Guid routeId, CancellationToken cancellationToken)
    {
        var route = await context.Routes.SingleAsync(x => x.Id == routeId, cancellationToken);
        route.IsPublic = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetSavedAsync(Guid routeId, string userId, bool value, CancellationToken cancellationToken)
    {
        var saved = await context.SavedRoutes.FindAsync([routeId, userId], cancellationToken);
        if (value && saved is null)
        {
            saved = new SavedRoute { RouteId = routeId, UserId = userId };
            context.SavedRoutes.Add(saved);
        }
        else if (!value && saved is not null)
            context.SavedRoutes.Remove(saved);
        else
            return;
        await SaveMembershipAsync(saved, value, cancellationToken);
    }

    private async Task SaveMembershipAsync(object entity, bool adding, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (adding && exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(entity).State = EntityState.Detached;
        }
        catch (DbUpdateConcurrencyException) when (!adding)
        {
            context.Entry(entity).State = EntityState.Detached;
        }
    }
}
