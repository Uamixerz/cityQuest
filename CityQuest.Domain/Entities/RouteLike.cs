namespace CityQuest.Domain.Entities;

public class RouteLike
{
    public Guid RouteId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
