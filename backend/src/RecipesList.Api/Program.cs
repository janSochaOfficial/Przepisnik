using Microsoft.EntityFrameworkCore;
using RecipesList.Api.Auth;
using RecipesList.Api.Configuration;
using RecipesList.Api.Features.Auth;
using RecipesList.Api.Features.Catalog;
using RecipesList.Api.Features.Favorites;
using RecipesList.Api.Features.Fridges;
using RecipesList.Api.Features.Images;
using RecipesList.Api.Features.Recipes;
using RecipesList.Api.Features.ShoppingLists;
using RecipesList.Api.Features.Transfer;
using RecipesList.Api.Features.Variants;
using RecipesList.Api.Hosting;
using RecipesList.Api.Middleware;
using RecipesList.Infrastructure;
using RecipesList.Infrastructure.Persistence;

// Hotspot file (plan §4.2): features do not edit it. They implement Add<Feature>/Map<Feature> in their own files.

DotEnvLocal.Load(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

// Per-worktree port (plan §4.3). Containers set ASPNETCORE_URLS / ASPNETCORE_HTTP_PORTS instead.
if (Environment.GetEnvironmentVariable("API_PORT") is { Length: > 0 } apiPort)
{
    builder.WebHost.UseUrls($"http://localhost:{apiPort}");
}

builder.Services.ConfigureHttpJsonOptions(options => JsonSetup.Configure(options.SerializerOptions));
builder.Services.AddApiProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddApiAuthentication(builder.Environment);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuth();
builder.Services.AddCatalog();
builder.Services.AddImages();
builder.Services.AddRecipes();
builder.Services.AddVariants();
builder.Services.AddFavorites();
builder.Services.AddFridges();
builder.Services.AddShoppingLists();
builder.Services.AddTransfer();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CsrfHeaderMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi("/api/openapi/{documentName}.json");
}

app.MapHealth();

var api = app.MapGroup("/api/v1")
    .RequireAuthorization()
    .AddEndpointFilter<OnboardingGateFilter>();

api.MapAuth();
api.MapCatalog();
api.MapImages();
api.MapRecipes();
api.MapVariants();
api.MapFavorites();
api.MapFridges();
api.MapShoppingLists();
api.MapTransfer();

await app.RunAsync();

/// <summary>Entry point; partial so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
