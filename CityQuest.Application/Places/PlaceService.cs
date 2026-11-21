using System.ComponentModel.DataAnnotations;
using CityQuest.Application.Exceptions;
using CityQuest.Domain.Entities;

namespace CityQuest.Application.Places;

public class PlaceService(IPlaceRepository repository)
{
    public async Task<List<PlaceResponse>> GetAllAsync(string? category, string? search,
        CancellationToken cancellationToken)
    {
        if (search?.Length > 100)
            throw new ValidationException("Пошук має містити не більше 100 символів.");

        var places = await repository.GetAllAsync(category?.Trim().ToLowerInvariant(), search?.Trim(), cancellationToken);
        return places.Select(PlaceResponse.FromEntity).ToList();
    }

    public async Task<PlaceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var place = await FindAsync(id, cancellationToken);
        return PlaceResponse.FromEntity(place);
    }

    public async Task<PlaceResponse> CreateAsync(PlaceRequest request, CancellationToken cancellationToken)
    {
        var place = new Place();
        Apply(place, request);
        await repository.AddAsync(place, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return PlaceResponse.FromEntity(place);
    }

    public async Task UpdateAsync(Guid id, PlaceRequest request, CancellationToken cancellationToken)
    {
        var place = await FindAsync(id, cancellationToken);
        Apply(place, request);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var place = await FindAsync(id, cancellationToken);
        if (await repository.IsUsedInRoutesAsync(id, cancellationToken))
            throw new ConflictException("Локація використовується у збережених маршрутах.");
        repository.Remove(place);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetHintAsync(Guid id, CancellationToken cancellationToken)
    {
        var place = await FindAsync(id, cancellationToken);
        return place.Hint ?? throw new NotFoundException("Для цієї локації немає підказки.");
    }

    private async Task<Place> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("Локацію не знайдено.");

    private static void Apply(Place place, PlaceRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        place.Name = request.Name.Trim();
        place.Description = request.Description.Trim();
        place.Category = request.Category;
        place.Latitude = request.Latitude;
        place.Longitude = request.Longitude;
        place.VisitMinutes = request.VisitMinutes;
        place.Fact = Normalize(request.Fact);
        place.Challenge = Normalize(request.Challenge);
        place.Hint = Normalize(request.Hint);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
