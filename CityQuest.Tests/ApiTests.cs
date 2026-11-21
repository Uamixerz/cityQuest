using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CityQuest.Application.Places;
using CityQuest.DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CityQuest.Tests;

public class ApiTests
{
    [Theory]
    [InlineData("прогуля1", HttpStatusCode.OK)]
    [InlineData("ABCDEFG1", HttpStatusCode.OK)]
    [InlineData("прогулянка", HttpStatusCode.BadRequest)]
    [InlineData("12345678", HttpStatusCode.BadRequest)]
    [InlineData("abc1234", HttpStatusCode.BadRequest)]
    public async Task RegistrationRequiresEightCharactersALetterAndADigit(string password, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "walk@example.test", password });
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedFailureReturnsSafeProblemDetails()
    {
        await using var factory = new ApiFactory();
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped<IPlaceRepository>(_ =>
                throw new InvalidOperationException("Internal database connection details"))));
        var client = failingFactory.CreateClient();
        var response = await client.GetAsync("/api/places");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal database connection details", body);
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task CatalogIsPublicAndUnknownPlaceReturnsProblemDetails()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/places")).StatusCode);
        var response = await client.GetAsync($"/api/places/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task LongSearchReturnsBadRequest()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/places?search=" + new string('a', 101));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IdentityProtectsMutationsAndAdminCanManagePlaces()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var request = new PlaceRequest
        {
            Name = " Test place ", Description = "Description", Category = "nature",
            Latitude = 50.7, Longitude = 25.3, VisitMinutes = 10
        };
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/places", request)).StatusCode);

        const string permissions = "/api/account/permissions";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(permissions)).StatusCode);
        var credentials = new { email = "test@example.com", password = "Test-password42!" };
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/register", credentials)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", credentials);
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/places", request)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>(permissions)).GetProperty("canManagePlaces").GetBoolean());

        using (var scope = factory.Services.CreateScope())
            await DatabaseInitializer.GrantAdminAsync(scope.ServiceProvider, credentials.email);
        login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", credentials);
        login.EnsureSuccessStatusCode();
        tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.GetProperty("accessToken").GetString());

        var created = await client.PostAsJsonAsync("/api/places", request);
        Assert.True((await client.GetFromJsonAsync<JsonElement>(permissions)).GetProperty("canManagePlaces").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var place = await created.Content.ReadFromJsonAsync<PlaceResponse>();
        Assert.NotNull(place);
        Assert.Equal("Test place", place.Name);
        var path = $"/api/places/{place.Id}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        request.VisitMinutes = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, request)).StatusCode);
        request.VisitMinutes = 25;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(25, (await client.GetFromJsonAsync<PlaceResponse>(path))!.VisitMinutes);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = tokens.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task SwaggerIncludesCatalogAndIdentity()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/places", out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/auth/login", out _));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }
}
