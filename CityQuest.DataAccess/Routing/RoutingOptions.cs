namespace CityQuest.DataAccess.Routing;

public class RoutingOptions
{
    public string Provider { get; set; } = "Auto";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
