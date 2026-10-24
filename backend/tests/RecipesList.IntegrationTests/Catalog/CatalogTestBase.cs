using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.IntegrationTests.Auth;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Catalog;

/// <summary>Shared arrangement for the catalog tests: an empty database, a normal user and an admin.</summary>
public abstract class CatalogTestBase(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public const string AdminNickname = "szef";

    private WebApplicationFactory<Program>? _adminApp;

    protected ApiFactory Factory { get; } = factory;

    public async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        _adminApp = Factory.WithWebHostBuilder(builder => builder.UseSetting("Admin:Emails:0", $"{AdminNickname}@test.local"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_adminApp is not null)
        {
            await _adminApp.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    protected async Task<ApiClient> LoginAsync(string nickname)
    {
        var client = Factory.CreateApiClient();
        await client.LoginAsync(nickname);
        return client;
    }

    /// <summary>A client signed in as the configured admin (<c>szef@test.local</c>).</summary>
    protected async Task<ApiClient> LoginAdminAsync()
    {
        var client = _adminApp!.CreateApiClient();
        await client.LoginAsync(AdminNickname);
        client.Me.GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
        return client;
    }

    protected async Task<Guid> AddProductAsync(
        string name,
        MeasureKind kind = MeasureKind.Mass,
        DisplayClass display = DisplayClass.Weight,
        ProductCategory category = ProductCategory.Other,
        bool isVerified = false)
    {
        await using var db = Factory.CreateDbContext();
        var product = new Product
        {
            Name = name,
            NormalizedName = NameNormalizer.Normalize(name),
            Category = category,
            MeasureKind = kind,
            DisplayClass = display,
            IsVerified = isVerified,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }

    /// <summary>A valid <c>ProductWrite</c> body (a mass product) with optional overrides.</summary>
    protected static Dictionary<string, object?> ProductBody(string name, string category = "other", string measureKind = "mass", string displayClass = "weight", bool hasSizes = false, bool allowHalf = false) =>
        new()
        {
            ["name"] = name,
            ["category"] = category,
            ["measureKind"] = measureKind,
            ["displayClass"] = displayClass,
            ["hasSizes"] = hasSizes,
            ["allowHalf"] = allowHalf,
        };

    protected static string[] Names(JsonElement array) => [.. array.EnumerateArray().Select(e => e.GetProperty("name").GetString()!)];
}
