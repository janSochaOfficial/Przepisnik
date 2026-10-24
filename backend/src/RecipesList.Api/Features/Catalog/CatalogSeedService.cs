using RecipesList.Infrastructure.Seeding;

namespace RecipesList.Api.Features.Catalog;

/// <summary>
/// Seeds the catalog once at startup when <c>Seed:Enabled</c> is true (the default). The directory comes from
/// <c>Seed:Path</c>; without it the service looks for <c>shared/seed</c> next to the working directory or above it.
/// A failure is logged and never stops the API.
/// </summary>
internal sealed partial class CatalogSeedService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<CatalogSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Seed:Enabled", true))
        {
            return;
        }

        var directory = ResolveDirectory(configuration["Seed:Path"]);
        if (directory is null)
        {
            LogNoDirectory(logger);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync(directory, cancellationToken);
            LogSeeded(logger, result.Products, result.Tags, result.Utensils);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFailed(logger, e);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The configured directory, or the nearest <c>shared/seed</c> from the working directory upwards.</summary>
    internal static string? ResolveDirectory(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "shared", "seed");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog seed directory not found; set Seed__Path. Skipping the seed.")]
    private static partial void LogNoDirectory(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog seed: {Products} products, {Tags} tags, {Utensils} utensils inserted.")]
    private static partial void LogSeeded(ILogger logger, int products, int tags, int utensils);

    [LoggerMessage(Level = LogLevel.Error, Message = "Catalog seeding failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
