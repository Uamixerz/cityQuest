using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CityQuest.Application.Exceptions;
using CityQuest.Application.Routes;
using CityQuest.DataAccess.Routing;
using CityQuest.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CityQuest.Tests;

public class WalkingRouteServiceTests
{
    private static readonly Place Attraction = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Парк", Category = "nature",
        Latitude = 50.741, Longitude = 25.32, VisitMinutes = 10
    };

    private static RoutePlanRequest Request() => new()
    {
        StartLatitude = 50.74, StartLongitude = 25.32, Interests = ["nature"],
        TimeBudgetMinutes = 30, ReturnToStart = true
    };

    private const string Matrix = """
        {"durations":[[0,120],[180,0]],"distances":[[0,160],[240,0]]}
        """;

    private const string Directions = """
        {"features":[{"properties":{"segments":[{"duration":61,"distance":100},{"duration":121,"distance":200}]},
        "geometry":{"type":"LineString","coordinates":[[25.32,50.74],[25.32,50.741],[25.32,50.74]]}}]}
        """;

    private static WalkingRouteService Service(Handler handler, string provider = "OpenRouteService", string? key = "test-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.openrouteservice.org/") },
            new RoutePlanner(), Options.Create(new RoutingOptions { Provider = provider, ApiKey = key }),
            NullLogger<WalkingRouteService>.Instance);

    [Fact]
    public async Task RequestsWalkingRoadsAndUsesDirectionsTotalsAndGeometry()
    {
        var handler = new Handler(Matrix, Directions);
        var plan = await Service(handler).PlanAsync(Request(), [Attraction], default);
        Assert.False(plan.IsEstimated);
        Assert.Equal("ors-foot-walking-v1", plan.CalculationMethod);
        Assert.Equal(15, plan.TotalMinutes);
        Assert.Equal(5, plan.WalkingMinutes);
        Assert.Equal(3, plan.ReturnWalkingMinutes);
        Assert.Equal(300, plan.DistanceMeters);
        Assert.Equal(2, plan.Stops[0].WalkingMinutes);
        Assert.Equal("LineString", plan.Geometry!.Type);
        Assert.Equal(3, plan.Geometry.Coordinates.Length);
        Assert.Contains("OpenStreetMap", plan.Attribution);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/v2/matrix/foot-walking", handler.Requests[0].Path);
        Assert.Equal("/v2/directions/foot-walking/geojson", handler.Requests[1].Path);
        Assert.All(handler.Requests, x => Assert.Equal("test-key", x.Authorization));
        using var matrixBody = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(25.32, matrixBody.RootElement.GetProperty("locations")[0][0].GetDouble());
        Assert.Equal(50.74, matrixBody.RootElement.GetProperty("locations")[0][1].GetDouble());
        using var directionsBody = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal(3, directionsBody.RootElement.GetProperty("coordinates").GetArrayLength());
        Assert.True(directionsBody.RootElement.GetProperty("instructions").GetBoolean());
    }

    [Fact]
    public async Task UnreachableLegDoesNotBecomeAnEstimatedConnection()
    {
        var handler = new Handler("""{"durations":[[0,null],[180,0]],"distances":[[0,null],[240,0]]}""");
        await Assert.ThrowsAsync<RoutePlanningException>(() => Service(handler).PlanAsync(Request(), [Attraction], default));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DirectionsWithoutSegmentsReturnsSafeProviderError()
    {
        var response = JsonNode.Parse(Directions)!;
        response["features"]![0]!["properties"]!.AsObject().Remove("segments");
        await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            Service(new Handler(Matrix, response.ToJsonString())).PlanAsync(Request(), [Attraction], default));
    }

    [Theory]
    [InlineData(1, "There may be restrictions", "обмеження доступу")]
    [InlineData(2, "Tollways", "платні ділянки")]
    [InlineData(4, "Extra info requested but not available: roadaccessrestrictions", "не означає, що шлях перекритий")]
    [InlineData(4, "Extra info unavailable", "додаткової інформації")]
    [InlineData(99, "Unknown provider text", "код 99")]
    public async Task ProviderWarningsExplainTheirActualMeaning(int code, string message, string expected)
    {
        var response = JsonNode.Parse(Directions)!;
        response["features"]![0]!["properties"]!["warnings"] =
            JsonSerializer.SerializeToNode(new[] { new { code, message } });
        var plan = await Service(new Handler(Matrix, response.ToJsonString())).PlanAsync(Request(), [Attraction], default);
        Assert.Contains(plan.Warnings, x => x.Contains(expected));
    }

    [Fact]
    public async Task SkippedRoadSegmentsCannotBePresentedAsACompleteWalkingRoute()
    {
        var response = JsonNode.Parse(Directions)!;
        response["features"]![0]!["properties"]!["warnings"] =
            JsonSerializer.SerializeToNode(new[] { new { code = 3, message = "Skipped segments" } });
        await Assert.ThrowsAsync<RoutePlanningException>(() =>
            Service(new Handler(Matrix, response.ToJsonString())).PlanAsync(Request(), [Attraction], default));
    }

    [Fact]
    public async Task FinalDirectionsCannotExceedTimeBudget()
    {
        var handler = new Handler(Matrix, Directions.Replace("\"duration\":121", "\"duration\":1800"));
        await Assert.ThrowsAsync<RoutePlanningException>(() => Service(handler).PlanAsync(Request(), [Attraction], default));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task ProviderFailuresDoNotLeakBodyOrSilentlyFallback(int status)
    {
        var handler = new Handler("private provider details") { Status = (HttpStatusCode)status };
        var exception = await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            Service(handler).PlanAsync(Request(), [Attraction], default));
        Assert.DoesNotContain("private", exception.Message);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"durations\":[[0]],\"distances\":[[0]]}")]
    public async Task MalformedMatrixBecomesSafeProviderError(string body)
    {
        await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            Service(new Handler(body)).PlanAsync(Request(), [Attraction], default));
    }

    [Fact]
    public async Task MissingKeyOnlyUsesEstimateInAutoMode()
    {
        var handler = new Handler();
        var plan = await Service(handler, "Auto", null).PlanAsync(Request(), [Attraction], default);
        Assert.True(plan.IsEstimated);
        Assert.Null(plan.Geometry);
        await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            Service(handler, "OpenRouteService", null).PlanAsync(Request(), [Attraction], default));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TimeoutAndCallerCancellationAreDifferent()
    {
        var handler = new Handler { Failure = new TaskCanceledException() };
        await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            Service(handler).PlanAsync(Request(), [Attraction], default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service(handler).PlanAsync(Request(), [Attraction], cancellation.Token));
    }

    [Fact]
    public void PlannerUsesAsymmetricRoadCostsInsteadOfProximity()
    {
        var other = new Place
        {
            Id = Guid.NewGuid(), Name = "Доступний парк", Category = "nature",
            Latitude = 50.75, Longitude = 25.32, VisitMinutes = 10
        };
        var matrix = new WalkingMatrix();
        matrix.Legs[(Guid.Empty, Attraction.Id)] = new WalkingLeg(5, 400);
        matrix.Legs[(Attraction.Id, Guid.Empty)] = null;
        matrix.Legs[(Guid.Empty, other.Id)] = new WalkingLeg(6, 450);
        matrix.Legs[(other.Id, Guid.Empty)] = new WalkingLeg(8, 600);
        var plan = new RoutePlanner().Plan(Request(), [Attraction, other], default, matrix);
        Assert.Equal(other.Id, Assert.Single(plan.Stops).PlaceId);
        Assert.Equal(24, plan.TotalMinutes);
        Assert.False(plan.IsEstimated);
    }

    [Fact]
    public void RoadGeometryAndMethodSurviveStoredRouteMapping()
    {
        var route = new WalkingRoute
        {
            CalculationMethod = "ors-foot-walking-v1", Interests = ["nature"],
            GeometryJson = JsonSerializer.Serialize(new RouteGeometry("LineString", [[25.32, 50.74], [25.32, 50.741]])),
            Attribution = "Test attribution", RoadWarnings = ["Provider warning"],
            Stops = [new RouteStop { Category = "nature" }]
        };
        var mapped = RouteResponse.FromEntity(route, null, 0, false, false);
        Assert.False(mapped.Plan.IsEstimated);
        Assert.NotNull(mapped.Plan.Geometry);
        Assert.Contains("Provider warning", mapped.Plan.Warnings);
        Assert.Equal("Test attribution", mapped.Plan.Attribution);
        Assert.DoesNotContain(mapped.Plan.Warnings, x => x.Contains("річки"));
    }

    private class Handler(params string[] responses) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public Exception? Failure { get; init; }
        public List<(string Path, string Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Failure is not null)
                throw Failure;
            Requests.Add((request.RequestUri!.AbsolutePath, request.Headers.GetValues("Authorization").Single(),
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            var responseBody = responses[Requests.Count - 1];
            if (request.RequestUri.AbsolutePath.EndsWith("/geojson"))
            {
                using var body = JsonDocument.Parse(Requests[^1].Body);
                if (body.RootElement.TryGetProperty("instructions", out var instructions) && !instructions.GetBoolean())
                {
                    var json = JsonNode.Parse(responseBody)!;
                    json["features"]![0]!["properties"]!.AsObject().Remove("segments");
                    responseBody = json.ToJsonString();
                }
            }
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
