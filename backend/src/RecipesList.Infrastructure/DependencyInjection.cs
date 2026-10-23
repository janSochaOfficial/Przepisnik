using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Visibility;
using RecipesList.Infrastructure.Persistence;

namespace RecipesList.Infrastructure;

/// <summary>Registers persistence and the shared Core services. Feature services are registered by each feature.</summary>
public static class DependencyInjection
{
    /// <summary>Adds <see cref="AppDbContext"/> (Npgsql, snake_case), <see cref="IAppDbContext"/> and shared Core services.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured (ConnectionStrings__Default).");

        services.AddDbContext<AppDbContext>(options => ConfigureDbContext(options, connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IQuantityConverter, QuantityConverter>();
        services.AddScoped<VisibilityRules>();
        return services;
    }

    /// <summary>The provider configuration shared by the app, the design-time factory and the tests.</summary>
    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention();
    }
}
