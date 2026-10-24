using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;

namespace RecipesList.Infrastructure.Seeding;

/// <summary>How many rows a seeding pass inserted; rows that already exist are never touched.</summary>
public sealed record SeedResult(int Products, int Tags, int Utensils);

/// <summary>
/// Loads <c>products.json</c>, <c>tags.json</c> and <c>utensils.json</c> from a directory into the catalog (TECHNICAL
/// §3.4). Idempotent: a row is inserted only when no row has its normalized name, so admin edits are never overwritten
/// and a second run inserts nothing. A missing file is skipped.
/// </summary>
public sealed class CatalogSeeder(IAppDbContext db, TimeProvider time)
{
    public const string ProductsFile = "products.json";
    public const string TagsFile = "tags.json";
    public const string UtensilsFile = "utensils.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    /// <summary>Seeds the catalog from <paramref name="directory"/>.</summary>
    public async Task<SeedResult> SeedAsync(string directory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var products = await SeedProductsAsync(Path.Combine(directory, ProductsFile), ct);
        var tags = await SeedTagsAsync(Path.Combine(directory, TagsFile), ct);
        var utensils = await SeedUtensilsAsync(Path.Combine(directory, UtensilsFile), ct);
        return new SeedResult(products, tags, utensils);
    }

    private async Task<int> SeedProductsAsync(string path, CancellationToken ct)
    {
        var file = await ReadAsync<ProductsDocument>(path, ct);
        if (file?.Products is not { Count: > 0 } entries)
        {
            return 0;
        }

        var existing = await db.Products.Select(p => p.NormalizedName).ToListAsync(ct);
        var known = new HashSet<string>(existing, StringComparer.Ordinal);
        var added = 0;
        foreach (var entry in entries)
        {
            var input = new ProductInput(
                entry.Name, entry.Category, entry.MeasureKind, entry.DisplayClass, entry.HasSizes, entry.AllowHalf,
                entry.GramsPerMl, entry.GramsPerPiece, entry.DefaultPackageAmount, entry.PieceForms);
            try
            {
                ProductRules.Validate(input);
            }
            catch (DomainException e)
            {
                throw new InvalidOperationException($"Seed product '{entry.Name}' is invalid ({e.Code}).", e);
            }

            var name = ProductRules.CleanName(entry.Name);
            var normalized = NameNormalizer.Normalize(name);
            if (!known.Add(normalized))
            {
                continue;
            }

            db.Products.Add(new Product
            {
                Name = name,
                NormalizedName = normalized,
                Category = input.Category,
                MeasureKind = input.MeasureKind,
                DisplayClass = input.DisplayClass,
                HasSizes = input.HasSizes,
                AllowHalf = input.AllowHalf,
                GramsPerMl = input.GramsPerMl,
                GramsPerPiece = input.GramsPerPiece,
                DefaultPackageAmount = input.DefaultPackageAmount,
                PieceForms = input.PieceForms,
                CreatedById = null,
                CreatedAt = time.GetUtcNow(),
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> SeedTagsAsync(string path, CancellationToken ct)
    {
        var file = await ReadAsync<TagsDocument>(path, ct);
        if (file?.Tags is not { Count: > 0 } entries)
        {
            return 0;
        }

        var known = new HashSet<string>(await db.Tags.Select(t => t.NormalizedName).ToListAsync(ct), StringComparer.Ordinal);
        var added = 0;
        foreach (var entry in entries)
        {
            var name = ProductRules.ValidateSimpleName(entry.Name);
            var normalized = NameNormalizer.Normalize(name);
            if (!known.Add(normalized))
            {
                continue;
            }

            db.Tags.Add(new Tag { Name = name, NormalizedName = normalized, IsSystem = entry.IsSystem });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> SeedUtensilsAsync(string path, CancellationToken ct)
    {
        var file = await ReadAsync<UtensilsDocument>(path, ct);
        if (file?.Utensils is not { Count: > 0 } entries)
        {
            return 0;
        }

        var known = new HashSet<string>(await db.Utensils.Select(u => u.NormalizedName).ToListAsync(ct), StringComparer.Ordinal);
        var added = 0;
        foreach (var entry in entries)
        {
            var name = ProductRules.ValidateSimpleName(entry.Name);
            var normalized = NameNormalizer.Normalize(name);
            if (!known.Add(normalized))
            {
                continue;
            }

            db.Utensils.Add(new Utensil { Name = name, NormalizedName = normalized });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    // Shapes of shared/seed/*.schema.json. Unknown members (e.g. "$schema") are ignored.
    private sealed record ProductsDocument(List<ProductEntry>? Products);

    private sealed record ProductEntry(
        string Name,
        ProductCategory Category,
        MeasureKind MeasureKind,
        DisplayClass DisplayClass,
        bool HasSizes,
        bool AllowHalf,
        decimal? GramsPerMl,
        decimal? GramsPerPiece,
        decimal? DefaultPackageAmount,
        PluralForms? PieceForms);

    private sealed record TagsDocument(List<TagEntry>? Tags);

    private sealed record TagEntry(string Name, bool IsSystem);

    private sealed record UtensilsDocument(List<UtensilEntry>? Utensils);

    private sealed record UtensilEntry(string Name);
}
