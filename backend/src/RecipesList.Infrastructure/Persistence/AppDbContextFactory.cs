using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecipesList.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the API.
/// The connection string is only used by commands that touch a database (e.g. <c>database update</c>).
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=recipes;Username=recipes;Password=recipes";
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(builder, connectionString);
        return new AppDbContext(builder.Options);
    }
}
