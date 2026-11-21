using CityQuest.DataAccess.Identity;
using CityQuest.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CityQuest.DataAccess;

public class CityQuestDbContext(DbContextOptions<CityQuestDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Place> Places => Set<Place>();
    public DbSet<WalkingRoute> Routes => Set<WalkingRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<RouteLike> RouteLikes => Set<RouteLike>();
    public DbSet<SavedRoute> SavedRoutes => Set<SavedRoute>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CityQuestDbContext).Assembly);
    }
}
