using System.ComponentModel.DataAnnotations;

namespace CityQuest.Application.Places;

public class PlaceRequest : IValidatableObject
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required, RegularExpression("^(architecture|nature|monument|museum|cafe|restaurant)$")]
    public string Category { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [Range(1, 1440)]
    public int VisitMinutes { get; set; }

    [StringLength(2000)]
    public string? Fact { get; set; }

    [StringLength(1000)]
    public string? Challenge { get; set; }

    [StringLength(1000)]
    public string? Hint { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Hint) && string.IsNullOrWhiteSpace(Challenge))
            yield return new ValidationResult("Підказка має належати завданню.", [nameof(Hint)]);
    }
}
