using System.ComponentModel.DataAnnotations;
using CityQuest.Application.Exceptions;
using CityQuest.Domain;
using CityQuest.Domain.Entities;

namespace CityQuest.Application.Routes;

public class RoutePlanner
{
    public static readonly string[] EstimationWarnings =
    [
        "Час і відстань орієнтовні. Пішохідні дороги, річки, паркани та інші перешкоди не перевірені.",
        "Години роботи, доступність входів і наявність місць у закладах не перевірені."
    ];

    public static IReadOnlyList<string> GetWarnings(IEnumerable<string> interests, IEnumerable<string> categories, bool estimated = true)
    {
        string[] warnings = estimated ? EstimationWarnings :
        ["Час ходьби орієнтовний. Години роботи, доступність входів і наявність місць у закладах не перевірені."];
        var missing = interests.Except(categories).ToArray();
        return missing.Length == 0 ? warnings
            : [.. warnings, "Не всі інтереси вдалося включити в маршрут: " + string.Join(", ", missing) + "."];
    }

    public RoutePlanResponse Plan(RoutePlanRequest request, IReadOnlyList<Place> places,
        CancellationToken cancellationToken = default, WalkingMatrix? matrix = null)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        var nearby = places.Where(x => Distance(request.StartLatitude, request.StartLongitude,
                x.Latitude, x.Longitude) <= 25000)
            .OrderBy(x => Distance(request.StartLatitude, request.StartLongitude, x.Latitude, x.Longitude))
            .ThenBy(x => x.Id).ToList();
        if (request.PlaceIds is { } ids)
            return PlanSelected(request, nearby.Where(x => ids.Contains(x.Id)).ToList(), matrix, cancellationToken);
        var attractions = nearby.Where(x => request.Interests.Contains(x.Category)).Take(150).ToList();
        var food = nearby.Where(x => PlaceCategories.IsFood(x.Category)
            && (request.FoodType is null || x.Category == request.FoodType)).Take(30).ToList();

        if (attractions.Count == 0)
            throw new RoutePlanningException("У радіусі 25 км немає локацій за обраними інтересами.");
        if (request.IncludeFood && food.Count == 0)
            throw new RoutePlanningException("Поблизу немає закладу обраного типу. Змініть тип або вимкніть зупинку на їжу.");

        List<Place>? selected = null;
        var bestMinutes = int.MaxValue;
        foreach (var attraction in attractions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<List<Place>> seeds = request.IncludeFood
                ? food.SelectMany(x => new List<Place>[] { [attraction, x], [x, attraction] })
                : [[attraction]];
            foreach (var seed in seeds)
            {
                var minutes = Measure(request, seed, matrix)?.TotalMinutes ?? int.MaxValue;
                if (minutes <= request.TimeBudgetMinutes && minutes < bestMinutes)
                {
                    selected = seed;
                    bestMinutes = minutes;
                }
            }
        }

        if (selected is null)
            throw new RoutePlanningException("За вказаний час не вдається відвідати локацію з обраними умовами. Збільшіть час або змініть налаштування.");

        while (selected.Count < request.MaxStops)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<Place>? best = null;
            var bestNewCategory = false;
            var bestTotal = int.MaxValue;
            foreach (var place in attractions.Where(x => selected.All(s => s.Id != x.Id)))
            {
                var newCategory = selected.All(x => x.Category != place.Category);
                for (var index = 0; index <= selected.Count; index++)
                {
                    var proposal = selected.ToList();
                    proposal.Insert(index, place);
                    var minutes = Measure(request, proposal, matrix)?.TotalMinutes ?? int.MaxValue;
                    if (minutes > request.TimeBudgetMinutes)
                        continue;
                    if (best is null || (newCategory && !bestNewCategory)
                        || (newCategory == bestNewCategory && minutes < bestTotal))
                    {
                        best = proposal;
                        bestTotal = minutes;
                        bestNewCategory = newCategory;
                    }
                }
            }
            if (best is null)
                break;
            selected = best;
        }

        return Measure(request, selected, matrix)!;
    }

    private static RoutePlanResponse PlanSelected(RoutePlanRequest request, List<Place> places, WalkingMatrix? matrix,
        CancellationToken cancellationToken)
    {
        if (places.Count != request.PlaceIds!.Length)
            throw new RoutePlanningException("Не всі обрані локації доступні в радіусі 25 км від старту.");
        RoutePlanResponse? best = null;
        foreach (var first in places)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<Place> ordered = [first];
            var remaining = places.Where(x => x.Id != first.Id).ToList();
            while (remaining.Count > 0)
            {
                var last = ordered[^1];
                var next = remaining.Select(p => (Place: p, Leg: matrix is null
                    ? Estimate(last.Latitude, last.Longitude, p.Latitude, p.Longitude) : matrix.Get(last.Id, p.Id)))
                    .Where(x => x.Leg is not null).OrderBy(x => x.Leg!.Minutes).ThenBy(x => x.Leg!.Meters)
                    .FirstOrDefault();
                if (next.Place is null) break;
                ordered.Add(next.Place);
                remaining.Remove(next.Place);
            }
            if (remaining.Count > 0) continue;
            var plan = Measure(request, ordered, matrix);
            if (plan is not null && (best is null || (plan.WalkingMinutes, plan.DistanceMeters, plan.Stops[0].DistanceMeters)
                .CompareTo((best.WalkingMinutes, best.DistanceMeters, best.Stops[0].DistanceMeters)) < 0))
                best = plan;
        }
        if (best is null) throw new RoutePlanningException("Не вдалося з’єднати всі обрані точки пішохідними дорогами.");
        if (best.TotalMinutes > request.TimeBudgetMinutes)
            throw new RoutePlanningException("Усі обрані точки разом не вкладаються у заданий час.");
        return best;
    }

    private static RoutePlanResponse? Measure(RoutePlanRequest request, IReadOnlyList<Place> stops, WalkingMatrix? matrix)
    {
        var latitude = request.StartLatitude;
        var longitude = request.StartLongitude;
        var result = new List<RouteStopResponse>();
        var previousId = Guid.Empty;
        foreach (var place in stops)
        {
            var leg = matrix is null
                ? Estimate(latitude, longitude, place.Latitude, place.Longitude)
                : matrix.Get(previousId, place.Id);
            if (leg is null)
                return null;
            result.Add(new RouteStopResponse(result.Count + 1, place.Id, place.Name, place.Category,
                place.Latitude, place.Longitude, place.VisitMinutes, leg.Minutes, leg.Meters));
            latitude = place.Latitude;
            longitude = place.Longitude;
            previousId = place.Id;
        }
        var returnLeg = !request.ReturnToStart ? new WalkingLeg(0, 0)
            : matrix is null ? Estimate(latitude, longitude, request.StartLatitude, request.StartLongitude)
            : matrix.Get(previousId, Guid.Empty);
        if (returnLeg is null)
            return null;
        var returnMeters = returnLeg.Meters;
        var returnMinutes = returnLeg.Minutes;
        var walkingMinutes = result.Sum(x => x.WalkingMinutes) + returnMinutes;
        return new RoutePlanResponse(request.StartLatitude, request.StartLongitude, request.ReturnToStart,
            request.TimeBudgetMinutes, walkingMinutes + result.Sum(x => x.VisitMinutes), walkingMinutes,
            result.Sum(x => x.DistanceMeters) + returnMeters, returnMinutes, returnMeters,
            matrix is null ? "estimated-walk-v1" : "ors-foot-walking-v1", matrix is null,
            GetWarnings(request.Interests, result.Select(x => x.Category), matrix is null), result);
    }

    private static WalkingLeg Estimate(double lat1, double lon1, double lat2, double lon2)
    {
        var meters = EstimatedMeters(lat1, lon1, lat2, lon2);
        return new WalkingLeg(WalkingMinutes(meters), meters);
    }

    private static int WalkingMinutes(int meters) => (int)Math.Ceiling(meters / 80d);

    private static int EstimatedMeters(double lat1, double lon1, double lat2, double lon2) =>
        (int)Math.Ceiling(Distance(lat1, lon1, lat2, lon2) * 1.3);

    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * radians / 2), 2)
            + Math.Cos(lat1 * radians) * Math.Cos(lat2 * radians)
            * Math.Pow(Math.Sin((lon2 - lon1) * radians / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
