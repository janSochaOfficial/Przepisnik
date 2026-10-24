using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Images;
using RecipesList.Core.Recipes;
using RecipesList.Core.Transfer;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Transfer;

/// <summary>The export document and the file name it is offered under.</summary>
internal sealed record ExportResult(RecipeExportDocument Document, string FileName);

/// <summary>
/// Builds the export file of a visible recipe (API.md §8): every id becomes an opaque key (<c>i1</c>, <c>s1</c>, <c>p1</c>,
/// <c>img1</c> … assigned in a fixed traversal order, so equal content gives equal keys), products carry their full
/// properties and photos are embedded as base64 WebP. Variants are limited to those visible to the caller.
/// </summary>
internal sealed partial class RecipeExporter(
    IAppDbContext db,
    VisibilityRules visibility,
    ICurrentUser currentUser,
    IImageStorage storage,
    TimeProvider time)
{
    private const string FallbackSlug = "przepis";
    private const int MaxSlugLength = 60;

    public async Task<ExportResult> ExportAsync(Guid recipeId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var recipe = await visibility.VisibleRecipes(userId)
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Ingredients)
            .Include(r => r.Steps)
            .Include(r => r.Tags).ThenInclude(t => t.Tag)
            .Include(r => r.Utensils).ThenInclude(u => u.Utensil)
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == recipeId, ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);

        var variants = await visibility.VisibleVariants(userId)
            .AsNoTracking()
            .Include(v => v.Author)
            .Where(v => v.RecipeId == recipeId)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .ToListAsync(ct);

        var products = await LoadProductsAsync(recipe, variants, ct);
        var utensilNames = await LoadUtensilNamesAsync(variants, ct);

        var keys = new Keys();
        var exportedRecipe = MapRecipe(recipe, keys, products);
        var exportedVariants = variants.Select(v => MapVariant(v, keys, products, utensilNames)).ToList();

        var document = new RecipeExportDocument
        {
            Format = ExportFormat.Name,
            FormatVersion = ExportFormat.Version,
            ExportedAt = time.GetUtcNow(),
            Products = [.. keys.Products.Order.Select(id => MapProduct(products[id], keys.Products.For(id)))],
            Images = await ReadImagesAsync(keys.Images.Order, keys.Images, ct),
            Recipe = exportedRecipe,
            Variants = exportedVariants,
        };
        return new ExportResult(document, Slug(recipe.Title) + ".json");
    }

    private static ExportRecipe MapRecipe(Recipe recipe, Keys keys, Dictionary<Guid, Product> products)
    {
        var (description, originalAuthor) = ImportFooter.Strip(recipe.Description);
        var ingredients = recipe.Ingredients.OrderBy(i => i.Position).Select(i => new ExportIngredient
        {
            Key = keys.Ingredients.For(i.Id),
            ProductKey = keys.Products.For(products[i.ProductId].Id),
            Quantity = i.Quantity,
            Note = i.Note,
            Optional = i.Optional,
        }).ToList();

        // Keys of the cover and the step photos are assigned in this order, which the importer reproduces.
        var coverKey = recipe.CoverImageId is { } cover ? keys.Images.For(cover) : null;
        return new ExportRecipe
        {
            Key = "r1",
            Title = recipe.Title,
            Description = description,
            CoverImageKey = coverKey,
            AuthorNickname = originalAuthor ?? recipe.Author?.Nickname ?? "",
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
            Tags = [.. recipe.Tags.Select(t => t.Tag!).Where(t => !t.IsSystem).Select(t => t.Name).Order(StringComparer.Ordinal)],
            Utensils = [.. recipe.Utensils.Select(u => u.Utensil!.Name).Order(StringComparer.Ordinal)],
            Ingredients = ingredients,
            PrepSteps = Steps(recipe, StepSection.Prep, keys),
            InstructionSteps = Steps(recipe, StepSection.Instructions, keys),
        };
    }

    private static List<ExportStep> Steps(Recipe recipe, StepSection section, Keys keys) =>
        [.. recipe.Steps.Where(s => s.Section == section).OrderBy(s => s.Position).Select(s => new ExportStep
        {
            Key = keys.Steps.For(s.Id),
            Blocks = Blocks(s.Blocks, keys),
        })];

    private static List<ExportBlock> Blocks(IEnumerable<ContentBlock> blocks, Keys keys) =>
        [.. blocks.Select(b => b switch
        {
            TextBlock t => (ExportBlock)new ExportTextBlock(t.Text),
            ImageBlock i => new ExportImageBlock(keys.Images.For(i.ImageId), i.Caption),
            _ => throw new InvalidOperationException("Unknown content block."),
        })];

    private static ExportVariant MapVariant(Variant variant, Keys keys, Dictionary<Guid, Product> products, Dictionary<Guid, string> utensilNames)
    {
        var (description, originalAuthor) = ImportFooter.Strip(variant.Description);
        return new ExportVariant
        {
            Name = variant.Name,
            Description = description,
            AuthorNickname = originalAuthor ?? variant.Author?.Nickname ?? "",
            PrepMinutes = variant.PrepMinutes,
            CookMinutes = variant.CookMinutes,
            UtensilsAdded = [.. variant.UtensilIdsAdded.Select(id => utensilNames[id])],
            UtensilsRemoved = [.. variant.UtensilIdsRemoved.Select(id => utensilNames[id])],
            IngredientOps = [.. variant.IngredientOps.Select(op => MapIngredientOp(op, keys, products))],
            StepOps = [.. variant.StepOps.Select(op => MapStepOp(op, keys))],
        };
    }

    private static ExportIngredientOp MapIngredientOp(IngredientOp op, Keys keys, Dictionary<Guid, Product> products) => op switch
    {
        ModifyIngredientOp m => new ExportModifyIngredientOp(
            keys.Ingredients.For(m.TargetId), keys.Products.For(products[m.ProductId].Id), m.Quantity, m.Note, m.Optional),
        RemoveIngredientOp r => new ExportRemoveIngredientOp(keys.Ingredients.For(r.TargetId)),
        AddIngredientOp a => new ExportAddIngredientOp(
            keys.Ingredients.For(a.Id),
            a.AfterId is { } after ? keys.Ingredients.For(after) : null,
            keys.Products.For(products[a.ProductId].Id),
            a.Quantity,
            a.Note,
            a.Optional),
        _ => throw new InvalidOperationException("Unknown ingredient operation."),
    };

    private static ExportStepOp MapStepOp(StepOp op, Keys keys) => op switch
    {
        ReplaceStepOp r => new ExportReplaceStepOp(r.Section, keys.Steps.For(r.TargetId), Blocks(r.Blocks, keys)),
        RemoveStepOp r => new ExportRemoveStepOp(r.Section, keys.Steps.For(r.TargetId)),
        AddStepOp a => new ExportAddStepOp(
            a.Section, keys.Steps.For(a.Id), a.AfterId is { } after ? keys.Steps.For(after) : null, Blocks(a.Blocks, keys)),
        _ => throw new InvalidOperationException("Unknown step operation."),
    };

    private static ExportProduct MapProduct(Product p, string key) => new()
    {
        Key = key,
        Name = p.Name,
        Category = p.Category,
        MeasureKind = p.MeasureKind,
        DisplayClass = p.DisplayClass,
        HasSizes = p.HasSizes,
        AllowHalf = p.AllowHalf,
        GramsPerMl = p.GramsPerMl,
        GramsPerPiece = p.GramsPerPiece,
        DefaultPackageAmount = p.DefaultPackageAmount,
        PieceForms = p.PieceForms,
    };

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(Recipe recipe, List<Variant> variants, CancellationToken ct)
    {
        var ids = recipe.Ingredients.Select(i => i.ProductId)
            .Concat(variants.SelectMany(v => v.IngredientOps).Select(op => op switch
            {
                ModifyIngredientOp m => (Guid?)m.ProductId,
                AddIngredientOp a => a.ProductId,
                _ => null,
            }).OfType<Guid>())
            .Distinct()
            .ToList();
        return await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }

    private async Task<Dictionary<Guid, string>> LoadUtensilNamesAsync(List<Variant> variants, CancellationToken ct)
    {
        var ids = variants.SelectMany(v => v.UtensilIdsAdded.Concat(v.UtensilIdsRemoved)).Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.Utensils.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private async Task<Dictionary<string, ExportImage>> ReadImagesAsync(IReadOnlyList<Guid> ids, KeyMap images, CancellationToken ct)
    {
        var result = new Dictionary<string, ExportImage>(ids.Count, StringComparer.Ordinal);
        foreach (var id in ids)
        {
            await using var stream = storage.Open(id, ImageSize.Full)
                ?? throw new InvalidOperationException($"Image {id} is referenced but missing from storage.");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            result[images.For(id)] = new ExportImage
            {
                ContentType = ExportFormat.ImageContentType,
                DataBase64 = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length),
            };
        }

        return result;
    }

    private static string Slug(string title)
    {
        var slug = NonSlug().Replace(NameNormalizer.Normalize(title), "-").Trim('-');
        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength].Trim('-');
        }

        return slug.Length == 0 ? FallbackSlug : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();

    /// <summary>Id → key maps of one export, each numbering its keys in the order ids are first met.</summary>
    private sealed class Keys
    {
        public KeyMap Ingredients { get; } = new("i");

        public KeyMap Steps { get; } = new("s");

        public KeyMap Products { get; } = new("p");

        public KeyMap Images { get; } = new("img");
    }

    private sealed class KeyMap(string prefix)
    {
        private readonly Dictionary<Guid, string> _keys = [];
        private readonly List<Guid> _order = [];

        public IReadOnlyList<Guid> Order => _order;

        public string For(Guid id)
        {
            if (!_keys.TryGetValue(id, out var key))
            {
                key = prefix + (_keys.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _keys[id] = key;
                _order.Add(id);
            }

            return key;
        }
    }
}
