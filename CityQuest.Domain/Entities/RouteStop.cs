namespace CityQuest.Domain.Entities;

public class RouteStop
{
    public Guid RouteId { get; set; }
    public int Position { get; set; }
    public Guid PlaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int VisitMinutes { get; set; }
    public int WalkingMinutes { get; set; }
    public int DistanceMeters { get; set; }
}
