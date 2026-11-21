namespace CityQuest.Domain.Entities;

public class WalkingRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public bool ReturnToStart { get; set; }
    public int TimeBudgetMinutes { get; set; }
    public int TotalMinutes { get; set; }
    public int WalkingMinutes { get; set; }
    public int DistanceMeters { get; set; }
    public int ReturnWalkingMinutes { get; set; }
    public int ReturnDistanceMeters { get; set; }
    public string[] Interests { get; set; } = [];
    public bool IncludeFood { get; set; }
    public string? FoodType { get; set; }
    public string CalculationMethod { get; set; } = "estimated-walk-v1";
    public string? GeometryJson { get; set; }
    public string? Attribution { get; set; }
    public string[] RoadWarnings { get; set; } = [];
    public List<RouteStop> Stops { get; set; } = [];
    public List<RouteLike> Likes { get; set; } = [];
    public List<SavedRoute> Saves { get; set; } = [];
}
