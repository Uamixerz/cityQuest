using CityQuest.Application.Places;
using CityQuest.Application.Routes;
using CityQuest.DataAccess;
using CityQuest.DataAccess.Identity;
using CityQuest.DataAccess.Repositories;
using CityQuest.DataAccess.Routing;
using Microsoft.Extensions.Options;
using CityQuest.WebApi.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("CityQuest");
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddDbContext<CityQuestDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.")));
builder.Services.AddScoped<IPlaceRepository, PlaceRepository>();
builder.Services.AddScoped<PlaceService>();
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<RouteService>();
builder.Services.AddSingleton<RoutePlanner>();
builder.Services.AddOptions<RoutingOptions>().Bind(builder.Configuration.GetSection("Routing"))
    .Validate(x => x.Provider is "Auto" or "Estimated" or "OpenRouteService", "Unknown routing provider.")
    .Validate(x => x.TimeoutSeconds is >= 1 and <= 120, "Routing timeout must be 1–120 seconds.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IWalkingRouteService, WalkingRouteService>((services, client) =>
{
    client.BaseAddress = new Uri("https://api.openrouteservice.org/");
    client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<IOptions<RoutingOptions>>().Value.TimeoutSeconds);
    client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
});
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireDigit = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddPasswordValidator<LetterPasswordValidator>()
    .AddEntityFrameworkStores<CityQuestDbContext>();
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CityQuest API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Access token from POST /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

if (args.Contains("--initialize-database"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CityQuestDbContext>();
    await context.Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("SeedAdmin:Enabled"))
        await DatabaseInitializer.SeedAdminAsync(scope.ServiceProvider,
            builder.Configuration["SeedAdmin:Email"] ?? "",
            builder.Configuration["SeedAdmin:Password"] ?? "");
    if (app.Environment.IsDevelopment())
        await DatabaseInitializer.SeedPlacesAsync(context);
    app.Logger.LogInformation("Database initialized");
    return;
}

var adminArgument = Array.IndexOf(args, "--grant-admin");
if (adminArgument >= 0)
{
    if (adminArgument + 1 >= args.Length)
        throw new ArgumentException("Specify an existing user's email after --grant-admin.");
    using var scope = app.Services.CreateScope();
    await DatabaseInitializer.GrantAdminAsync(scope.ServiceProvider, args[adminArgument + 1]);
    app.Logger.LogInformation("Administrator role assigned");
    return;
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (!app.Environment.IsDevelopment() && builder.Configuration.GetValue("HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();
app.MapGet("/api/account/permissions", (System.Security.Claims.ClaimsPrincipal user) =>
    new { canManagePlaces = user.IsInRole("Admin") }).RequireAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/ready", async (CityQuestDbContext context, CancellationToken cancellationToken) =>
    await context.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: 503, title: "Database unavailable"));
app.Run();

public partial class Program;
