namespace CityQuest.Domain.Entities;

public class SavedRoute
{
    public Guid RouteId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
