using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CityQuest.Application.Exceptions;
using CityQuest.Application.Routes;
using CityQuest.Domain;
using CityQuest.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CityQuest.DataAccess.Routing;

public class WalkingRouteService(HttpClient client, RoutePlanner planner,
    IOptions<RoutingOptions> options, ILogger<WalkingRouteService> logger) : IWalkingRouteService
{
    public async Task<RoutePlanResponse> PlanAsync(RoutePlanRequest request, IReadOnlyList<Place> places,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        
        if (settings.Provider == "Estimated" || (settings.Provider == "Auto" && string.IsNullOrWhiteSpace(settings.ApiKey)))
            return planner.Plan(request, places, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new RoutingUnavailableException("Сервіс пішохідних маршрутів не налаштовано.");

        var nearby = places.Where(x => RoutePlanner.Distance(request.StartLatitude, request.StartLongitude,
                x.Latitude, x.Longitude) <= 25000)
            .OrderBy(x => RoutePlanner.Distance(request.StartLatitude, request.StartLongitude, x.Latitude, x.Longitude))
            .ThenBy(x => x.Id).ToList();
        var attractions = nearby.Where(x => request.Interests.Contains(x.Category)).Take(30).ToList();
        
        var food = nearby.Where(x => request.IncludeFood && PlaceCategories.IsFood(x.Category)
            && (request.FoodType is null || request.FoodType == x.Category)).Take(10).ToList();
        
        if (request.PlaceIds is null && (attractions.Count == 0 || (request.IncludeFood && food.Count == 0)))
            throw new RoutePlanningException("Поблизу немає локацій або закладу за обраними умовами.");
        
        var candidates = request.PlaceIds is { } selected
            ? nearby.Where(x => selected.Contains(x.Id)).ToList()
            : attractions.Concat(food).ToList();
        
        if (request.PlaceIds is not null && candidates.Count != request.PlaceIds.Length)
            throw new RoutePlanningException("Не всі обрані локації доступні в радіусі 25 км від старту.");
        
        double[][] locations = [[request.StartLongitude, request.StartLatitude],
            .. candidates.Select(x => new[] { x.Longitude, x.Latitude })];
        Guid[] ids = [Guid.Empty, .. candidates.Select(x => x.Id)];

        try
        {
            using var matrixJson = await SendAsync("v2/matrix/foot-walking", new
            {
                locations, metrics = new[] { "duration", "distance" }, units = "m"
            }, cancellationToken);
            var matrix = ReadMatrix(matrixJson.RootElement, ids);
            var plan = planner.Plan(request, candidates, cancellationToken, matrix);
            double[][] coordinates = [[request.StartLongitude, request.StartLatitude],
                .. plan.Stops.Select(x => new[] { x.Longitude, x.Latitude })];
            if (request.ReturnToStart)
                coordinates = [.. coordinates, [request.StartLongitude, request.StartLatitude]];

            using var directionsJson = await SendAsync("v2/directions/foot-walking/geojson", new
            {
                coordinates, instructions = true, units = "m",
                radiuses = Enumerable.Repeat(100, coordinates.Length).ToArray()
            }, cancellationToken);
            
            return ApplyDirections(plan, directionsJson.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Walking route provider timed out");
            throw new RoutingUnavailableException("Сервіс маршрутів не відповів вчасно. Спробуйте пізніше.");
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Walking route provider connection failed");
            throw new RoutingUnavailableException("Сервіс маршрутів тимчасово недоступний.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            logger.LogWarning("Walking route provider returned an invalid response");
            throw InvalidResponse();
        }
    }

    private async Task<JsonDocument> SendAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Authorization", options.Value.ApiKey);
        request.Content = JsonContent.Create(body);
        
        using var response = await client.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Walking route provider returned HTTP {Status}", (int)response.StatusCode);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
                throw new RoutePlanningException("Не вдалося прокласти пішохідний шлях. Перевірте старт і входи до локацій.");
            throw new RoutingUnavailableException("Сервіс маршрутів недоступний або перевищено ліміт запитів. Спробуйте пізніше.");
        }
        
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static WalkingMatrix ReadMatrix(JsonElement root, Guid[] ids)
    {
        var durations = root.GetProperty("durations");
        var distances = root.GetProperty("distances");
        
        if (durations.GetArrayLength() != ids.Length || distances.GetArrayLength() != ids.Length)
            throw InvalidResponse();
        
        var matrix = new WalkingMatrix();
        
        for (var from = 0; from < ids.Length; from++)
        {
            if (durations[from].GetArrayLength() != ids.Length || distances[from].GetArrayLength() != ids.Length)
                throw InvalidResponse();
            
            for (var to = 0; to < ids.Length; to++)
            {
                matrix.Legs[(ids[from], ids[to])] = durations[from][to].ValueKind == JsonValueKind.Null
                    || distances[from][to].ValueKind == JsonValueKind.Null
                    ? null : ReadLeg(durations[from][to], distances[from][to]);
            }
        }
        return matrix;
    }

    private static RoutePlanResponse ApplyDirections(RoutePlanResponse plan, JsonElement root)
    {
        var features = root.GetProperty("features");
        
        if (features.GetArrayLength() != 1)
            throw InvalidResponse();
        
        var feature = features[0];
        var properties = feature.GetProperty("properties");
        var segments = properties.GetProperty("segments");
        
        if (segments.GetArrayLength() != plan.Stops.Count + (plan.ReturnToStart ? 1 : 0))
            throw InvalidResponse();
        
        var legs = segments.EnumerateArray().Select(x => ReadLeg(x.GetProperty("duration"), x.GetProperty("distance"))).ToArray();
        var stops = plan.Stops.Select((x, index) => x with
        {
            WalkingMinutes = legs[index].Minutes, DistanceMeters = legs[index].Meters
        }).ToList();
        
        var returnLeg = plan.ReturnToStart ? legs[^1] : new WalkingLeg(0, 0);
        var walkingMinutes = legs.Sum(x => x.Minutes);
        var totalMinutes = walkingMinutes + stops.Sum(x => x.VisitMinutes);
        
        if (totalMinutes > plan.TimeBudgetMinutes)
            throw new RoutePlanningException("Уточнений шлях дорогами перевищує доступний час. Збільшіть бюджет часу або зменште кількість зупинок.");

        var geometry = feature.GetProperty("geometry");
        if (geometry.GetProperty("type").GetString() != "LineString")
            throw InvalidResponse();
        
        var points = geometry.GetProperty("coordinates").EnumerateArray().Select(x =>
        {
            if (x.GetArrayLength() < 2)
                throw InvalidResponse();
            var lon = x[0].GetDouble();
            var lat = x[1].GetDouble();
            if (!double.IsFinite(lon) || !double.IsFinite(lat) || lon is < -180 or > 180 || lat is < -90 or > 90)
                throw InvalidResponse();
            return new[] { lon, lat };
        }).ToArray();
        
        if (points.Length < 2)
            throw InvalidResponse();
        
        var warnings = plan.Warnings.ToList();
        if (properties.TryGetProperty("warnings", out var providerWarnings))
        {
            foreach (var warning in providerWarnings.EnumerateArray())
            {
                var code = warning.TryGetProperty("code", out var codeValue) && codeValue.TryGetInt32(out var number)
                    ? number : 0;
                
                if (code == 3)
                    throw new RoutePlanningException("Сервіс пропустив частину шляху. Не вдалося підтвердити суцільний пішохідний маршрут.");
                
                var message = code switch
                {
                    1 => "На деяких ділянках можуть бути обмеження доступу. Перевірте знаки та дозволені проходи.",
                    2 => "Маршрут містить платні ділянки. Перевірте умови проходу.",
                    4 when warning.TryGetProperty("message", out var detail)
                        && detail.GetString()?.Contains("roadaccessrestrictions", StringComparison.OrdinalIgnoreCase) == true
                        => "Сервіс не надав додаткових даних про обмеження доступу на дорогах. Це не означає, що шлях перекритий; перевіряйте доступність проходів на місці.",
                    4 => "Частина додаткової інформації про маршрут недоступна. Геометрію шляху отримано.",
                    _ => $"Сервіс карт повернув попередження (код {code}). Перевірте шлях перед прогулянкою."
                };
                if (!warnings.Contains(message))
                    warnings.Add(message);
            }
        }
        return plan with
        {
            TotalMinutes = totalMinutes, WalkingMinutes = walkingMinutes, DistanceMeters = legs.Sum(x => x.Meters),
            ReturnWalkingMinutes = returnLeg.Minutes, ReturnDistanceMeters = returnLeg.Meters,
            Stops = stops, Geometry = new RouteGeometry("LineString", points), Warnings = warnings,
            Attribution = "© openrouteservice.org by HeiGIT | Map data © OpenStreetMap contributors"
        };
    }

    private static WalkingLeg ReadLeg(JsonElement duration, JsonElement distance)
    {
        if (!duration.TryGetDouble(out var seconds) || !distance.TryGetDouble(out var meters)
            || !double.IsFinite(seconds) || !double.IsFinite(meters)
            || seconds is < 0 or > 10000000 || meters is < 0 or > 10000000)
            throw InvalidResponse();
        return new WalkingLeg((int)Math.Ceiling(seconds / 60), (int)Math.Ceiling(meters));
    }

    private static RoutingUnavailableException InvalidResponse() =>
        new("Сервіс маршрутів повернув некоректні дані. Спробуйте пізніше.");
}
