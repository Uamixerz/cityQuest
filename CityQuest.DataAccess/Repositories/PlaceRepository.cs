using CityQuest.Application.Places;
using CityQuest.Application.Exceptions;
using CityQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CityQuest.DataAccess.Repositories;

public class PlaceRepository(CityQuestDbContext context) : IPlaceRepository
{
    public Task<List<Place>> GetAllAsync(string? category, string? search, CancellationToken cancellationToken)
    {
        var query = context.Places.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
        }

        return query.OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<Place?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Places.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(Place place, CancellationToken cancellationToken) =>
        await context.Places.AddAsync(place, cancellationToken);

    public void Remove(Place place) => context.Places.Remove(place);

    public Task<bool> IsUsedInRoutesAsync(Guid id, CancellationToken cancellationToken) =>
        context.RouteStops.AnyAsync(x => x.PlaceId == id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_RouteStops_Places_PlaceId" })
        {
            throw new ConflictException("Локація використовується у збережених маршрутах.");
        }
    }
}
