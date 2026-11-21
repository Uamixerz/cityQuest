using CityQuest.Domain.Entities;

namespace CityQuest.Application.Places;

public record PlaceResponse(Guid Id, string Name, string Description, string Category,
    double Latitude, double Longitude, int VisitMinutes, string? Fact, string? Challenge, bool HasHint)
{
    public static PlaceResponse FromEntity(Place place) => new(place.Id, place.Name,
        place.Description, place.Category, place.Latitude, place.Longitude, place.VisitMinutes,
        place.Fact, place.Challenge, !string.IsNullOrWhiteSpace(place.Hint));
}
