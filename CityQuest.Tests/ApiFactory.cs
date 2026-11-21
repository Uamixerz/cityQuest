using CityQuest.DataAccess;
using CityQuest.DataAccess.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CityQuest.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.Configure<RoutingOptions>(options => { options.Provider = "Estimated"; options.ApiKey = null; });
            services.RemoveAll<DbContextOptions<CityQuestDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CityQuestDbContext>>();
            services.AddDbContext<CityQuestDbContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }
}
