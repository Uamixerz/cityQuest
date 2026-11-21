using CityQuest.DataAccess.Identity;
using CityQuest.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityQuest.DataAccess;

public static class DatabaseInitializer
{
    public static async Task SeedAdminAsync(IServiceProvider services, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Seed administrator email and password are required.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
            EnsureSucceeded(await users.CreateAsync(user));
        }

        await GrantAdminAsync(services, email);
    }

    public static async Task SeedPlacesAsync(CityQuestDbContext context)
    {
        Place[] places =
        [
            new Place { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Замок Любарта", Description = "Демонстраційна локація історичного центру Луцька.", Category = "architecture", Latitude = 50.7389, Longitude = 25.3239, VisitMinutes = 20 },
            new Place { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Вулиця Лесі Українки", Description = "Демонстраційна зупинка для міської прогулянки.", Category = "architecture", Latitude = 50.7444, Longitude = 25.3220, VisitMinutes = 10 },
            new Place { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Центральний парк", Description = "Демонстраційна зелена локація. Точку входу потрібно уточнити.", Category = "nature", Latitude = 50.7380, Longitude = 25.3350, VisitMinutes = 15 },
            new Place { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Демо-кафе (тестова точка)", Description = "Вигаданий заклад для перевірки маршруту з їжею.", Category = "cafe", Latitude = 50.7410, Longitude = 25.3230, VisitMinutes = 30 },
            new Place { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Демо-ресторан (тестова точка)", Description = "Вигаданий заклад для перевірки маршруту з їжею.", Category = "restaurant", Latitude = 50.7420, Longitude = 25.3240, VisitMinutes = 45 }
        ];
        var ids = await context.Places.Select(x => x.Id).ToListAsync();
        context.Places.AddRange(places.Where(x => !ids.Contains(x.Id)));
        await context.SaveChangesAsync();
    }

    public static async Task GrantAdminAsync(IServiceProvider services, string email)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Register the user before assigning the Admin role.");
        if (!await roles.RoleExistsAsync("Admin"))
            EnsureSucceeded(await roles.CreateAsync(new IdentityRole("Admin")));
        if (!await users.IsInRoleAsync(user, "Admin"))
            EnsureSucceeded(await users.AddToRoleAsync(user, "Admin"));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
