using CityQuest.Domain.Entities;

namespace CityQuest.Application.Places;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllAsync(string? category, string? search, CancellationToken cancellationToken);
    Task<Place?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Place place, CancellationToken cancellationToken);
    void Remove(Place place);
    Task<bool> IsUsedInRoutesAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
