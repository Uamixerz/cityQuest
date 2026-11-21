using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CityQuest.DataAccess;
using CityQuest.DataAccess.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CityQuest.Tests;

public class AdminSeedTests
{
    [Fact]
    public async Task SeededAdminCanLoginAndRegistrationStillRejectsShortPasswords()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            await DatabaseInitializer.SeedAdminAsync(scope.ServiceProvider, "admin@admin.com", "123123");
            await DatabaseInitializer.SeedAdminAsync(scope.ServiceProvider, "admin@admin.com", "123123");
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Equal(1, await users.Users.CountAsync(x => x.Email == "admin@admin.com"));
        }

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false",
            new { email = "admin@admin.com", password = "123123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.GetProperty("accessToken").GetString());
        var permissions = await client.GetFromJsonAsync<JsonElement>("/api/account/permissions");
        Assert.True(permissions.GetProperty("canManagePlaces").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register",
            new { email = "other@example.com", password = "123123" })).StatusCode);
    }

    [Fact]
    public async Task RepeatedSeedPreservesChangedPassword()
    {
        await using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        await DatabaseInitializer.SeedAdminAsync(scope.ServiceProvider, "admin@admin.com", "123123");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync("admin@admin.com"))!;
        Assert.True((await users.ChangePasswordAsync(user, "123123", "NewPassword42")).Succeeded);

        await DatabaseInitializer.SeedAdminAsync(scope.ServiceProvider, "admin@admin.com", "123123");
        Assert.True(await users.CheckPasswordAsync(user, "NewPassword42"));
        Assert.False(await users.CheckPasswordAsync(user, "123123"));
    }
}
