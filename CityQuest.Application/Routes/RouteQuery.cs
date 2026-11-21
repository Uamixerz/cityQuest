using System.ComponentModel.DataAnnotations;

namespace CityQuest.Application.Routes;

public class RouteQuery
{
    [Range(1, 10000)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 20;

    [RegularExpression("^(architecture|nature|monument|museum)$")]
    public string? Category { get; set; }

    [Range(1, 720)]
    public int? MaxMinutes { get; set; }

    public bool? IncludeFood { get; set; }

    [Required, RegularExpression("^(popular|newest)$")]
    public string Sort { get; set; } = "popular";
}
