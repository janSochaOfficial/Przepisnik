using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Images;
using RecipesList.Core.Recipes;
using RecipesList.Core.Transfer;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Transfer;

/// <summary>The outcome of importing a file as variants of an existing recipe.</summary>
internal sealed record VariantImportOutcome(Guid RecipeId, IReadOnlyList<Guid> CreatedVariantIds, int Skipped);

/// <summary>
/// Import of a file as variants of an existing recipe (PROJECT §4.7, D-35). The file's recipe becomes the variant
/// "Wersja @&lt;author&gt;" and each file variant another variant, each aligned with the target by
/// <see cref="ImportAlignment"/> and diffed by the variant engine. Catalog entries are matched or created exactly as
/// the normal import does. A candidate whose resolved form equals the target, an existing variant of it the caller
/// can see, or a variant created earlier in the same import is skipped.
/// </summary>
internal sealed partial class RecipeImporter
{
    /// <summary>The name of the variant created from the file's recipe.</summary>
    public static string RecipeVariantName(string authorNickname) => "Wersja @" + authorNickname;

    /// <summary>
    /// Adds the file's recipe and variants as private variants of <paramref name="targetId"/> owned by the caller.
    /// 404 <c>recipe.not_found</c> when the target is not visible, 409 <c>recipe.archived</c> when it is archived.
    /// </summary>
    public async Task<VariantImportOutcome> ImportAsVariantsAsync(Guid targetId, RecipeExportDocument document, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var target = await LoadTargetAsync(targetId, ct);
        ExportValidator.EnsureValid(document);
        var stepImageKeys = StepImageKeys(document);
        var images = DecodeImages(document, stepImageKeys);

        return await InTransactionAsync(async storedIds =>
        {
            var products = await ResolveProductsAsync(document, ct);
            var units = await ResolveUnitsAsync(document, products, ct);
            var refs = new FileRefs(products, units);

            // Photos identical to ones the target already shows are reused; the others get a placeholder id and are
            // stored only when a created variant uses them.
            var existingVariants = await visibility.VisibleVariants(userId).AsNoTracking()
                .Where(v => v.RecipeId == targetId)
                .ToListAsync(ct);
            var (imageIds, pending) = await MatchImagesAsync(images, target, existingVariants, ct);

            var (fileRecipe, fileVariants) = await BuildFileRecipeAsync(document, imageIds, refs, ct);
            var candidates = new List<(string Name, string Description, ResolvedRecipeModel Resolved)>
            {
                (RecipeVariantName(document.Recipe.AuthorNickname),
                    ImportFooter.Append(document.Recipe.Description, document.Recipe.AuthorNickname, ExportValidator.MaxDescription),
                    engine.Resolve(fileRecipe, null)),
            };
            candidates.AddRange(fileVariants.Select(v => (v.Name, v.Description ?? "", engine.Resolve(fileRecipe, v))));

            var targetResolved = engine.Resolve(target, null);
            var known = new List<ResolvedRecipeModel> { targetResolved };
            known.AddRange(existingVariants.Select(v => engine.Resolve(target, v)));

            var takenNames = (await db.Variants.AsNoTracking()
                .Where(v => v.RecipeId == targetId && v.AuthorId == userId)
                .Select(v => v.Name)
                .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

            var now = time.GetUtcNow();
            var created = new List<Variant>();
            var skipped = 0;
            foreach (var (name, description, resolved) in candidates)
            {
                var diff = engine.Diff(target, ImportAlignment.Align(targetResolved, resolved));
                var variant = new Variant
                {
                    RecipeId = targetId,
                    AuthorId = userId,
                    Name = name,
                    Description = description,
                    Visibility = VisibilityLevel.Private,
                    IngredientOps = [.. diff.IngredientOps],
                    StepOps = [.. diff.StepOps],
                    UtensilIdsAdded = [.. diff.UtensilIdsAdded],
                    UtensilIdsRemoved = [.. diff.UtensilIdsRemoved],
                    PrepMinutes = diff.PrepMinutes,
                    CookMinutes = diff.CookMinutes,
                    ServingsOverride = diff.ServingsOverride,
                };
                var result = engine.Resolve(target, variant);
                if (diff.IsEmpty || known.Exists(k => ImportAlignment.HasSameContent(k, result)))
                {
                    skipped++;
                    continue;
                }

                known.Add(result);
                variant.Name = UniqueName(name.Trim(), takenNames);
                variant.CreatedAt = now.AddMicroseconds(created.Count);
                variant.UpdatedAt = variant.CreatedAt;
                created.Add(variant);
            }

            await StoreUsedImagesAsync(created, pending, images, storedIds, ct);
            db.Variants.AddRange(created);
            await db.SaveChangesAsync(ct);
            return new VariantImportOutcome(targetId, [.. created.Select(v => v.Id)], skipped);
        });
    }

    // One query per collection, like the variant service, so the three collections don't multiply rows.
    private async Task<Recipe> LoadTargetAsync(Guid targetId, CancellationToken ct)
    {
        var recipe = await visibility.VisibleRecipes(currentUser.Id).AsNoTracking().SingleOrDefaultAsync(r => r.Id == targetId, ct)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        if (recipe.IsArchived)
        {
            throw new ConflictException(ErrorCodes.RecipeArchived);
        }

        recipe.Ingredients = await db.RecipeIngredients.AsNoTracking().Where(i => i.RecipeId == targetId).OrderBy(i => i.Position).ToListAsync(ct);
        recipe.Steps = await db.RecipeSteps.AsNoTracking().Where(s => s.RecipeId == targetId).OrderBy(s => s.Position).ToListAsync(ct);
        recipe.Utensils = await db.RecipeUtensils.AsNoTracking().Where(u => u.RecipeId == targetId).ToListAsync(ct);
        return recipe;
    }

    // The cover is not part of a variant, so only photos inside steps are imported.
    private static List<string> StepImageKeys(RecipeExportDocument document) =>
        [.. ExportValidator.ReferencedImageKeys(document with { Recipe = document.Recipe with { CoverImageKey = null } }).Distinct(StringComparer.Ordinal)];

    private static IEnumerable<Guid> StepImages(IEnumerable<ContentBlock> blocks) => blocks.OfType<ImageBlock>().Select(b => b.ImageId);

    private static IEnumerable<Guid> OpImages(IEnumerable<StepOp> ops) => ops.SelectMany(op => op switch
    {
        ReplaceStepOp r => StepImages(r.Blocks),
        AddStepOp a => StepImages(a.Blocks),
        _ => [],
    });

    /// <summary>
    /// Maps each file photo to a photo of the target (or of one of its visible variants) with the same bytes as its
    /// export, so a file exported from the target compares equal; the others get a placeholder id, returned with
    /// their keys in <c>Pending</c>.
    /// </summary>
    private async Task<(Dictionary<string, Guid> Ids, Dictionary<Guid, string> Pending)> MatchImagesAsync(
        Dictionary<string, byte[]> images,
        Recipe target,
        List<Variant> variants,
        CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var pending = new Dictionary<Guid, string>();
        if (images.Count == 0)
        {
            return (result, pending);
        }

        var candidates = target.Steps.SelectMany(s => StepImages(s.Blocks)).Concat(variants.SelectMany(v => OpImages(v.StepOps))).Distinct().ToList();
        var known = new List<(Guid Id, byte[] Bytes)>();
        foreach (var id in candidates)
        {
            await using var stream = storage.Open(id, ImageSize.Full);
            if (stream is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            known.Add((id, buffer.ToArray()));
        }

        foreach (var (key, bytes) in images)
        {
            var match = known.FindIndex(k => k.Bytes.AsSpan().SequenceEqual(bytes));
            if (match >= 0)
            {
                result[key] = known[match].Id;
                continue;
            }

            var placeholder = Guid.CreateVersion7();
            result[key] = placeholder;
            pending[placeholder] = key;
        }

        return (result, pending);
    }

    // The file's recipe and variants in memory: rows get fresh ids, so the variants resolve against the file's recipe.
    private async Task<(Recipe Recipe, List<Variant> Variants)> BuildFileRecipeAsync(
        RecipeExportDocument document,
        Dictionary<string, Guid> imageIds,
        FileRefs refs,
        CancellationToken ct)
    {
        var source = document.Recipe;
        var utensils = await ResolveUtensilsAsync(source.Utensils, ct);
        var input = new RecipeWriteInput(
            source.Title,
            null,
            null,
            source.PrepMinutes,
            source.CookMinutes,
            source.Servings,
            [],
            utensils,
            [.. source.Ingredients.Select(i => new IngredientWriteInput(null, refs.Products[i.ProductKey].Id, ToQuantity(i.Quantity, refs.Units), i.Note, i.Optional))],
            [.. source.PrepSteps.Select(s => new StepWriteInput(null, Blocks(s.Blocks, imageIds)))],
            [.. source.InstructionSteps.Select(s => new StepWriteInput(null, Blocks(s.Blocks, imageIds)))]);
        RecipeInputValidator.Validate(input).ThrowIfAny();

        var recipe = new Recipe
        {
            Title = source.Title.Trim(),
            NormalizedTitle = NameNormalizer.Normalize(source.Title),
            PrepMinutes = source.PrepMinutes,
            CookMinutes = source.CookMinutes,
            Servings = source.Servings,
        };
        var baseIngredients = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var i = 0; i < source.Ingredients.Count; i++)
        {
            var row = source.Ingredients[i];
            var product = refs.Products[row.ProductKey];
            var quantity = ToQuantity(row.Quantity, refs.Units);
            var ingredient = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                Position = i,
                ProductId = product.Id,
                Quantity = quantity,
                BaseAmount = BaseAmount(quantity, product, refs, $"recipe.ingredients[{i}].quantity"),
                Note = Clean(row.Note),
                Optional = row.Optional,
            };
            recipe.Ingredients.Add(ingredient);
            baseIngredients[row.Key] = ingredient.Id;
        }

        var baseSteps = new Dictionary<string, Guid>(StringComparer.Ordinal);
        AddSteps(StepSection.Prep, source.PrepSteps);
        AddSteps(StepSection.Instructions, source.InstructionSteps);
        recipe.Utensils = [.. utensils.Select(id => new RecipeUtensil { RecipeId = recipe.Id, UtensilId = id })];

        var variants = new List<Variant>(document.Variants.Count);
        foreach (var file in document.Variants)
        {
            var addedIngredients = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var addedSteps = new Dictionary<string, Guid>(StringComparer.Ordinal);
            variants.Add(new Variant
            {
                RecipeId = recipe.Id,
                Name = file.Name.Trim(),
                Description = ImportFooter.Append(file.Description, file.AuthorNickname, ExportValidator.MaxDescription),
                PrepMinutes = file.PrepMinutes,
                CookMinutes = file.CookMinutes,
                ServingsOverride = file.ServingsOverride,
                UtensilIdsAdded = await ResolveUtensilsAsync(file.UtensilsAdded, ct),
                UtensilIdsRemoved = await ResolveUtensilsAsync(file.UtensilsRemoved, ct),
                IngredientOps = [.. file.IngredientOps.Select((op, i) => IngredientOp(op, i, baseIngredients, addedIngredients, refs))],
                StepOps = [.. file.StepOps.Select(op => StepOp(op, baseSteps, addedSteps, imageIds))],
            });
        }

        return (recipe, variants);

        void AddSteps(StepSection section, IReadOnlyList<ExportStep> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var step = new RecipeStep { RecipeId = recipe.Id, Section = section, Position = i, Blocks = Blocks(steps[i].Blocks, imageIds) };
                recipe.Steps.Add(step);
                baseSteps[steps[i].Key] = step.Id;
            }
        }
    }

    // Stores the new photos the created variants use and swaps their placeholder ids for the stored ones.
    private async Task StoreUsedImagesAsync(
        List<Variant> created,
        Dictionary<Guid, string> pending,
        Dictionary<string, byte[]> images,
        List<Guid> storedIds,
        CancellationToken ct)
    {
        var used = created.SelectMany(v => OpImages(v.StepOps)).Where(pending.ContainsKey).Distinct().ToList();
        if (used.Count == 0)
        {
            return;
        }

        var stored = await StoreImagesAsync(used.ToDictionary(id => pending[id], id => images[pending[id]], StringComparer.Ordinal), storedIds, ct);
        var replacement = used.ToDictionary(id => id, id => stored[pending[id]]);
        foreach (var variant in created)
        {
            variant.StepOps = [.. variant.StepOps.Select(op => op switch
            {
                ReplaceStepOp r => r with { Blocks = Remap(r.Blocks, replacement) },
                AddStepOp a => a with { Blocks = Remap(a.Blocks, replacement) },
                _ => op,
            })];
        }
    }

    private static List<ContentBlock> Remap(IReadOnlyList<ContentBlock> blocks, Dictionary<Guid, Guid> replacement) =>
        [.. blocks.Select(b => b is ImageBlock image && replacement.TryGetValue(image.ImageId, out var id) ? image with { ImageId = id } : b)];
}
