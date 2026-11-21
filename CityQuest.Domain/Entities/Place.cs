namespace CityQuest.Domain.Entities;

public class Place
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int VisitMinutes { get; set; }
    public string? Fact { get; set; }
    public string? Challenge { get; set; }
    public string? Hint { get; set; }
}
