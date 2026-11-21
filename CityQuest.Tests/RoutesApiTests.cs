using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CityQuest.Application.Places;
using CityQuest.Application.Routes;
using CityQuest.DataAccess;
using CityQuest.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CityQuest.Tests;

public class RoutesApiTests
{
    private static CreateRouteRequest Request(bool isPublic = false) => new()
    {
        Name = "Прогулянка Луцьком", IsPublic = isPublic,
        StartLatitude = 50.74, StartLongitude = 25.32,
        TimeBudgetMinutes = 120, Interests = ["architecture", "nature"], IncludeFood = true
    };

    private static async Task<Guid> SeedAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CityQuestDbContext>();
        var place = new Place
        {
            Name = "Пам’ятка", Description = "Опис", Category = "architecture",
            Latitude = 50.74, Longitude = 25.32, VisitMinutes = 15,
            Fact = "Факт про місце", Challenge = "Знайди деталь на фасаді", Hint = "Подивись над входом"
        };
        context.Places.AddRange(place,
            new Place { Name = "Парк", Category = "nature", Latitude = 50.741, Longitude = 25.32, VisitMinutes = 10 },
            new Place { Name = "Кафе", Category = "cafe", Latitude = 50.7405, Longitude = 25.32, VisitMinutes = 30 });
        await context.SaveChangesAsync();
        return place.Id;
    }

    private static async Task<HttpClient> LoginAsync(ApiFactory factory, string email, bool admin = false)
    {
        var client = factory.CreateClient();
        var credentials = new { email, password = "Test-password42!" };
        (await client.PostAsJsonAsync("/api/auth/register", credentials)).EnsureSuccessStatusCode();
        if (admin)
        {
            using var scope = factory.Services.CreateScope();
            await DatabaseInitializer.GrantAdminAsync(scope.ServiceProvider, email);
        }
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", credentials);
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<RouteResponse> CreateAsync(HttpClient client, bool isPublic)
    {
        var response = await client.PostAsJsonAsync("/api/routes", Request(isPublic));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RouteResponse>())!;
    }

    [Fact]
    public async Task ChangingStartReordersOnlySelectedPlacesAndPreservesTheSavedRoute()
    {
        await using var factory = new ApiFactory();
        var first = await SeedAsync(factory);
        var owner = await LoginAsync(factory, "planner@example.com");
        var places = await owner.GetFromJsonAsync<List<PlaceResponse>>("/api/places");
        var cafe = places!.Single(x => x.Category == "cafe");
        var request = new CreateRouteRequest { Name = "Власний", PlaceIds = [cafe.Id, first],
            StartLatitude = 50.74, StartLongitude = 25.32, IsPublic = true };
        var created = await owner.PostAsJsonAsync("/api/routes", request);
        created.EnsureSuccessStatusCode();
        var route = (await created.Content.ReadFromJsonAsync<RouteResponse>())!;
        Assert.Equal(new[]{first,cafe.Id}, route.Plan.Stops.Select(x => x.PlaceId));
        var visitor = await LoginAsync(factory,"walker@example.com");
        var path = $"/api/routes/{route.Id}";
        (await visitor.PutAsync(path + "/saved",null)).EnsureSuccessStatusCode();
        request.StartLatitude = cafe.Latitude;
        Assert.Equal(HttpStatusCode.NotFound,(await visitor.PutAsJsonAsync(path,request)).StatusCode);
        (await owner.PutAsJsonAsync(path,request)).EnsureSuccessStatusCode();
        var updated = (await visitor.GetFromJsonAsync<RouteResponse>(path))!;
        Assert.True(updated.IsSaved);
        Assert.Equal(new[]{cafe.Id,first}, updated.Plan.Stops.Select(x => x.PlaceId));
        Assert.Equal(cafe.Latitude, updated.Plan.StartLatitude);
    }

    [Fact]
    public async Task PublicRoutesCanBeLikedRankedAndSavedWithoutDuplicates()
    {
        await using var factory = new ApiFactory();
        await SeedAsync(factory);
        var owner = await LoginAsync(factory, "owner@example.com");
        var visitor = await LoginAsync(factory, "visitor@example.com");
        var anonymous = factory.CreateClient();
        var preview = await anonymous.PostAsJsonAsync("/api/routes/preview", Request());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(0, (await owner.GetFromJsonAsync<RoutePage>("/api/routes/mine"))!.TotalCount);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/routes", Request())).StatusCode);

        var popular = await CreateAsync(owner, true);
        await CreateAsync(owner, true);
        var path = $"/api/routes/{popular.Id}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsync(path + "/like", null)).StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await visitor.PutAsync(path + "/like", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await visitor.PutAsync(path + "/saved", null)).StatusCode);
        }
        var detail = (await visitor.GetFromJsonAsync<RouteResponse>(path))!;
        Assert.Equal(1, detail.LikeCount);
        Assert.True(detail.IsLiked);
        Assert.True(detail.IsSaved);
        Assert.False(detail.IsOwner);
        var listing = (await anonymous.GetFromJsonAsync<RoutePage>("/api/routes?sort=popular&pageSize=1"))!;
        Assert.Equal(2, listing.TotalCount);
        Assert.Equal(popular.Id, Assert.Single(listing.Items).Id);
        Assert.Single((await visitor.GetFromJsonAsync<RoutePage>("/api/routes/saved"))!.Items);
        Assert.Empty((await owner.GetFromJsonAsync<RoutePage>("/api/routes/saved"))!.Items);
        Assert.Empty((await visitor.GetFromJsonAsync<RoutePage>("/api/routes/mine"))!.Items);
        Assert.Empty((await visitor.GetFromJsonAsync<RoutePage>("/api/routes?includeFood=false"))!.Items);
        Assert.Empty((await visitor.GetFromJsonAsync<RoutePage>("/api/routes?maxMinutes=15"))!.Items);
        Assert.Empty((await visitor.GetFromJsonAsync<RoutePage>("/api/routes?category=museum"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/routes?page=0")).StatusCode);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await visitor.DeleteAsync(path + "/like")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await visitor.DeleteAsync(path + "/saved")).StatusCode);
        }
        Assert.Equal(0, (await visitor.GetFromJsonAsync<RouteResponse>(path))!.LikeCount);
        Assert.Empty((await visitor.GetFromJsonAsync<RoutePage>("/api/routes/saved"))!.Items);
    }

    [Fact]
    public async Task PrivateRoutesAreVisibleOnlyToOwnerUntilPublished()
    {
        await using var factory = new ApiFactory();
        await SeedAsync(factory);
        var owner = await LoginAsync(factory, "owner@example.com");
        var visitor = await LoginAsync(factory, "visitor@example.com");
        var anonymous = factory.CreateClient();
        var route = await CreateAsync(owner, false);
        var path = $"/api/routes/{route.Id}";
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PutAsync(path + "/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PutAsync(path + "/saved", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PutAsync(path + "/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsync(path + "/like", null)).StatusCode);
        Assert.Empty((await anonymous.GetFromJsonAsync<RoutePage>("/api/routes"))!.Items);
        Assert.Single((await owner.GetFromJsonAsync<RoutePage>("/api/routes/mine"))!.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync(path + "/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Single((await anonymous.GetFromJsonAsync<RoutePage>("/api/routes"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PutAsync(path + "/publish", null)).StatusCode);
    }

    [Fact]
    public async Task PlaceContentHasSeparateHintAndSavedRoutesKeepTheirSnapshot()
    {
        await using var factory = new ApiFactory();
        var placeId = await SeedAsync(factory);
        var admin = await LoginAsync(factory, "admin@example.com", true);
        var anonymous = factory.CreateClient();
        var path = $"/api/places/{placeId}";
        var place = (await anonymous.GetFromJsonAsync<PlaceResponse>(path))!;
        Assert.Equal("Факт про місце", place.Fact);
        Assert.NotNull(place.Challenge);
        Assert.True(place.HasHint);
        var json = await anonymous.GetFromJsonAsync<JsonElement>(path);
        Assert.False(json.TryGetProperty("hint", out _));
        var hint = await anonymous.GetFromJsonAsync<JsonElement>(path + "/hint");
        Assert.Equal("Подивись над входом", hint.GetProperty("hint").GetString());

        var route = await CreateAsync(admin, true);
        var update = new PlaceRequest
        {
            Name = "Оновлена пам’ятка", Description = "Опис", Category = "monument",
            Latitude = 50.745, Longitude = 25.325, VisitMinutes = 90, Hint = "Без завдання"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, update)).StatusCode);
        update.Hint = null;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(path, update)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(path + "/hint")).StatusCode);
        var stored = (await anonymous.GetFromJsonAsync<RouteResponse>($"/api/routes/{route.Id}"))!;
        var stop = Assert.Single(stored.Plan.Stops, x => x.PlaceId == placeId);
        Assert.Equal("Пам’ятка", stop.Name);
        Assert.Equal(15, stop.VisitMinutes);
        Assert.Equal(route.Plan.TotalMinutes, stored.Plan.TotalMinutes);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ImpossibleAndInvalidPlansReturnExplicitErrors()
    {
        await using var factory = new ApiFactory();
        await SeedAsync(factory);
        var client = factory.CreateClient();
        var request = Request();
        request.FoodType = "restaurant";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync("/api/routes/preview", request)).StatusCode);
        request.Interests = ["unknown"];
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/routes/preview", request)).StatusCode);
        request.Interests = [];
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/routes/preview", request)).StatusCode);
    }
}
