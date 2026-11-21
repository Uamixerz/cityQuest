using System.ComponentModel.DataAnnotations;
using CityQuest.Domain;

namespace CityQuest.Application.Routes;

public class RoutePlanRequest : IValidatableObject
{
    [Range(-90, 90)]
    public double StartLatitude { get; set; }

    [Range(-180, 180)]
    public double StartLongitude { get; set; }

    [Range(15, 720)]
    public int TimeBudgetMinutes { get; set; } = 120;

    [Required, MaxLength(4)]
    public string[] Interests { get; set; } = [];

    [MinLength(1), MaxLength(20)]
    public Guid[]? PlaceIds { get; set; }

    public bool IncludeFood { get; set; }

    [RegularExpression("^(cafe|restaurant)$")]
    public string? FoodType { get; set; }

    public bool ReturnToStart { get; set; }

    [Range(1, 20)]
    public int MaxStops { get; set; } = 8;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PlaceIds is null && Interests is { Length: 0 })
            yield return new ValidationResult("Оберіть хоча б один інтерес.", [nameof(Interests)]);
        if (PlaceIds is not null && (PlaceIds.Contains(Guid.Empty) || PlaceIds.Distinct().Count() != PlaceIds.Length))
            yield return new ValidationResult("Оберіть різні локації.", [nameof(PlaceIds)]);
        if (Interests is not null && Interests.Any(x => !PlaceCategories.Attractions.Contains(x)))
            yield return new ValidationResult("Невідома категорія інтересів.", [nameof(Interests)]);
        if (!IncludeFood && FoodType is not null)
            yield return new ValidationResult("Тип закладу можна вказати лише зі зупинкою на їжу.", [nameof(FoodType)]);
        if (IncludeFood && MaxStops < 2)
            yield return new ValidationResult("Для прогулянки з їжею потрібно щонайменше дві зупинки.", [nameof(MaxStops)]);
    }
}

public class CreateRouteRequest : RoutePlanRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsPublic { get; set; }
}
