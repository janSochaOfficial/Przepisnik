using Npgsql;
using Testcontainers.PostgreSql;

namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 18 container for the whole test run (Testcontainers picks a free port). Each <see cref="ApiFactory"/>
/// gets its own database on it, so test classes can run in parallel (plan §4.3, TECHNICAL §6).
/// </summary>
public static class PostgresContainer
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public static async Task<string> CreateDatabaseAsync(CancellationToken ct)
    {
        var container = await Container.Value;
        var name = $"test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync(ct);

        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name, IncludeErrorDetail = true }
            .ConnectionString;
    }

    /// <summary>Drops a database created by <see cref="CreateDatabaseAsync"/>.</summary>
    public static async Task DropDatabaseAsync(string connectionString)
    {
        var container = await Container.Value;
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder("postgres:18").Build();
        await container.StartAsync();
        return container;
    }
}
