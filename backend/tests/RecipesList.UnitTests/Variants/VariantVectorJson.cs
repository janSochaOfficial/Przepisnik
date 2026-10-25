using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

/// <summary>
/// Reads <c>shared/test-vectors/variants/*.json</c> cases into entities and writes engine results back in the vector
/// shape (<c>variants.schema.json</c>), so expected and actual can be compared as JSON.
/// </summary>
internal static class VariantVectorJson
{
    public static Recipe ReadBase(JsonElement b)
    {
        var recipe = new Recipe
        {
            Id = b.GetProperty("recipeId").GetGuid(),
            Title = b.GetProperty("title").GetString()!,
            NormalizedTitle = b.GetProperty("title").GetString()!,
            AuthorId = b.GetProperty("author").GetProperty("id").GetGuid(),
            CreatedAt = DateTimeOffset.Parse(b.GetProperty("createdAt").GetString()!, CultureInfo.InvariantCulture),
            PrepMinutes = OptionalInt(b.GetProperty("prepMinutes")),
            CookMinutes = OptionalInt(b.GetProperty("cookMinutes")),
        };

        var position = 0;
        foreach (var i in b.GetProperty("ingredients").EnumerateArray())
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = i.GetProperty("id").GetGuid(),
                RecipeId = recipe.Id,
                Position = position++,
                ProductId = i.GetProperty("product").GetProperty("id").GetGuid(),
                Quantity = ReadQuantity(i.GetProperty("quantity")),
                BaseAmount = OptionalDecimal(i.GetProperty("baseAmount")),
                Note = OptionalString(i.GetProperty("note")),
                Optional = i.GetProperty("optional").GetBoolean(),
            });
        }

        AddSteps(recipe, b.GetProperty("prepSteps"), StepSection.Prep);
        AddSteps(recipe, b.GetProperty("instructionSteps"), StepSection.Instructions);
        foreach (var u in b.GetProperty("utensilIds").EnumerateArray())
        {
            recipe.Utensils.Add(new RecipeUtensil { RecipeId = recipe.Id, UtensilId = u.GetGuid() });
        }

        return recipe;
    }

    public static Variant ReadVariant(JsonElement v, Guid recipeId) => new()
    {
        Id = v.GetProperty("id").GetGuid(),
        RecipeId = recipeId,
        Name = v.GetProperty("name").GetString()!,
        AuthorId = v.GetProperty("author").GetProperty("id").GetGuid(),
        CreatedAt = DateTimeOffset.Parse(v.GetProperty("createdAt").GetString()!, CultureInfo.InvariantCulture),
        IngredientOps = [.. v.GetProperty("ingredientOps").EnumerateArray().Select(ReadIngredientOp)],
        StepOps = [.. v.GetProperty("stepOps").EnumerateArray().Select(ReadStepOp)],
        UtensilIdsAdded = [.. v.GetProperty("utensilIdsAdded").EnumerateArray().Select(u => u.GetGuid())],
        UtensilIdsRemoved = [.. v.GetProperty("utensilIdsRemoved").EnumerateArray().Select(u => u.GetGuid())],
        PrepMinutes = OptionalInt(v.GetProperty("prepMinutes")),
        CookMinutes = OptionalInt(v.GetProperty("cookMinutes")),
    };

    /// <summary>The edited copy (VariantWrite.recipe shape) with base amounts computed like the service does.</summary>
    public static RecipeDraft ReadDraft(JsonElement e, IReadOnlyDictionary<Guid, JsonElement> products)
    {
        var converter = new QuantityConverter();
        return new RecipeDraft(
            [.. e.GetProperty("ingredients").EnumerateArray().Select(i =>
            {
                var productId = i.GetProperty("productId").GetGuid();
                var quantity = i.TryGetProperty("quantity", out var q) ? ReadQuantity(q) : null;
                return new IngredientDraft(
                    i.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetGuid() : null,
                    productId,
                    quantity,
                    converter.ToBaseAmount(quantity, ReadProduct(products[productId])),
                    i.TryGetProperty("note", out var note) ? OptionalString(note) : null,
                    i.GetProperty("optional").GetBoolean());
            })],
            ReadStepDrafts(e.GetProperty("prepSteps")),
            ReadStepDrafts(e.GetProperty("instructionSteps")),
            [.. e.GetProperty("utensilIds").EnumerateArray().Select(u => u.GetGuid())],
            e.TryGetProperty("prepMinutes", out var prep) ? OptionalInt(prep) : null,
            e.TryGetProperty("cookMinutes", out var cook) ? OptionalInt(cook) : null,
            Servings: null); // the vector schema has no servings (R2 covers them in VariantEngineServingsTests)
    }

    /// <summary>Every product object that appears anywhere in the case, by id (the vectors' product catalog).</summary>
    public static Dictionary<Guid, JsonElement> CollectProducts(JsonElement c)
    {
        var products = new Dictionary<Guid, JsonElement>();
        Walk(c);
        return products;

        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name == "product" && p.Value.ValueKind == JsonValueKind.Object)
                    {
                        products[p.Value.GetProperty("id").GetGuid()] = p.Value;
                    }

                    Walk(p.Value);
                }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray())
                {
                    Walk(item);
                }
            }
        }
    }

    public static Product ReadProduct(JsonElement p) => new()
    {
        Id = p.GetProperty("id").GetGuid(),
        Name = p.GetProperty("name").GetString()!,
        NormalizedName = p.GetProperty("name").GetString()!,
        MeasureKind = Enum.Parse<MeasureKind>(p.GetProperty("measureKind").GetString()!, ignoreCase: true),
        HasSizes = p.GetProperty("hasSizes").GetBoolean(),
        AllowHalf = p.GetProperty("allowHalf").GetBoolean(),
        GramsPerMl = OptionalDecimal(p.GetProperty("gramsPerMl")),
        GramsPerPiece = OptionalDecimal(p.GetProperty("gramsPerPiece")),
    };

    public static JsonObject WriteResolved(ResolvedRecipeModel r, IReadOnlyDictionary<Guid, JsonElement> products) => new()
    {
        ["ingredients"] = new JsonArray([.. r.Ingredients.Select(i => WriteIngredient(i, products))]),
        ["prepSteps"] = WriteSteps(r.PrepSteps),
        ["instructionSteps"] = WriteSteps(r.InstructionSteps),
        ["utensilIds"] = new JsonArray([.. r.UtensilIds.Select(u => (JsonNode)u.ToString())]),
        ["prepMinutes"] = r.PrepMinutes,
        ["cookMinutes"] = r.CookMinutes,
    };

    public static JsonObject WriteMatrix(VariantMatrixModel m, JsonElement c, IReadOnlyDictionary<Guid, JsonElement> products)
    {
        var b = c.GetProperty("base");
        var variants = c.GetProperty("variants").EnumerateArray().ToDictionary(v => v.GetProperty("id").GetGuid());
        return new JsonObject
        {
            ["recipeId"] = m.RecipeId.ToString(),
            ["variants"] = new JsonArray([.. m.Variants.Select(v =>
            {
                var source = v.VariantId is { } id ? variants[id] : b;
                return (JsonNode)new JsonObject
                {
                    ["id"] = v.VariantId?.ToString() ?? MatrixKeys.BaseVariantId,
                    ["name"] = source.GetProperty(v.VariantId is null ? "title" : "name").GetString(),
                    ["author"] = JsonNode.Parse(source.GetProperty("author").GetRawText()),
                    ["cookCount"] = source.GetProperty("cookCount").GetInt32(),
                    ["createdAt"] = source.GetProperty("createdAt").GetString(),
                    ["isFavorite"] = false,
                    ["productSwaps"] = new JsonArray([.. v.ProductSwaps.Select(s => (JsonNode)new JsonObject
                    {
                        ["fromProductId"] = s.FromProductId.ToString(),
                        ["toProductId"] = s.ToProductId.ToString(),
                    })]),
                };
            })]),
            ["slots"] = new JsonArray([.. m.Slots.Select(s => (JsonNode)new JsonObject
            {
                ["key"] = s.Key,
                ["label"] = products[s.ProductId].GetProperty("name").GetString(),
                ["options"] = new JsonArray([.. s.Options.Select(o => (JsonNode)new JsonObject
                {
                    ["key"] = o.Key,
                    ["ingredient"] = o.Ingredient is null ? null : WriteIngredient(o.Ingredient, products),
                    ["variantIds"] = new JsonArray([.. o.VariantIds.Select(id => (JsonNode)(id?.ToString() ?? MatrixKeys.BaseVariantId))]),
                })]),
            })]),
        };
    }

    /// <summary>The diff in the stored variant shape (the <c>variant</c> fields compared by diff tests).</summary>
    public static JsonObject WriteDiff(VariantDiff d) => new()
    {
        ["ingredientOps"] = new JsonArray([.. d.IngredientOps.Select(WriteIngredientOp)]),
        ["stepOps"] = new JsonArray([.. d.StepOps.Select(WriteStepOp)]),
        ["utensilIdsAdded"] = new JsonArray([.. d.UtensilIdsAdded.Select(u => (JsonNode)u.ToString())]),
        ["utensilIdsRemoved"] = new JsonArray([.. d.UtensilIdsRemoved.Select(u => (JsonNode)u.ToString())]),
        ["prepMinutes"] = d.PrepMinutes,
        ["cookMinutes"] = d.CookMinutes,
    };

    /// <summary>Compares JSON trees; numbers by decimal value. Returns the first difference, or null.</summary>
    public static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path = "$")
    {
        switch (expected, actual)
        {
            case (null, null):
                return null;
            case (null, _) or (_, null):
                return $"{path}: expected {expected?.ToJsonString() ?? "null"}, got {actual?.ToJsonString() ?? "null"}";
            case (JsonObject e, JsonObject a):
                foreach (var key in e.Select(p => p.Key).Union(a.Select(p => p.Key)))
                {
                    if (!e.ContainsKey(key) || !a.ContainsKey(key))
                    {
                        return $"{path}.{key}: present on one side only";
                    }

                    if (FirstDifference(e[key], a[key], $"{path}.{key}") is { } diff)
                    {
                        return diff;
                    }
                }

                return null;
            case (JsonArray e, JsonArray a):
                for (var i = 0; i < Math.Max(e.Count, a.Count); i++)
                {
                    if (i >= e.Count || i >= a.Count)
                    {
                        return $"{path}: expected {e.Count} items, got {a.Count}";
                    }

                    if (FirstDifference(e[i], a[i], $"{path}[{i}]") is { } diff)
                    {
                        return diff;
                    }
                }

                return null;
            case (JsonValue e, JsonValue a):
                var ek = e.GetValueKind();
                var ak = a.GetValueKind();
                if (ek == JsonValueKind.Number && ak == JsonValueKind.Number)
                {
                    return Number(e) == Number(a) ? null : $"{path}: expected {e}, got {a}";
                }

                return e.ToJsonString() == a.ToJsonString() ? null : $"{path}: expected {e.ToJsonString()}, got {a.ToJsonString()}";
            default:
                return $"{path}: expected {expected.ToJsonString()}, got {actual.ToJsonString()}";
        }
    }

    private static decimal Number(JsonValue v) => decimal.Parse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static JsonObject WriteIngredient(ResolvedIngredient i, IReadOnlyDictionary<Guid, JsonElement> products) => new()
    {
        ["id"] = i.Id.ToString(),
        ["product"] = JsonNode.Parse(products[i.ProductId].GetRawText()),
        ["quantity"] = WriteQuantity(i.Quantity),
        ["baseAmount"] = i.BaseAmount,
        ["note"] = i.Note,
        ["optional"] = i.Optional,
    };

    private static JsonNode WriteIngredientOp(IngredientOp op) => op switch
    {
        ModifyIngredientOp m => new JsonObject
        {
            ["op"] = "modify",
            ["targetId"] = m.TargetId.ToString(),
            ["productId"] = m.ProductId.ToString(),
            ["quantity"] = WriteQuantity(m.Quantity),
            ["baseAmount"] = m.BaseAmount,
            ["note"] = m.Note,
            ["optional"] = m.Optional,
        },
        RemoveIngredientOp r => new JsonObject { ["op"] = "remove", ["targetId"] = r.TargetId.ToString() },
        AddIngredientOp a => new JsonObject
        {
            ["op"] = "add",
            ["id"] = a.Id.ToString(),
            ["afterId"] = a.AfterId?.ToString(),
            ["productId"] = a.ProductId.ToString(),
            ["quantity"] = WriteQuantity(a.Quantity),
            ["baseAmount"] = a.BaseAmount,
            ["note"] = a.Note,
            ["optional"] = a.Optional,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private static JsonNode WriteStepOp(StepOp op) => op switch
    {
        ReplaceStepOp r => new JsonObject
        {
            ["op"] = "replace",
            ["section"] = Section(r.Section),
            ["targetId"] = r.TargetId.ToString(),
            ["blocks"] = WriteBlocks(r.Blocks),
        },
        RemoveStepOp r => new JsonObject { ["op"] = "remove", ["section"] = Section(r.Section), ["targetId"] = r.TargetId.ToString() },
        AddStepOp a => new JsonObject
        {
            ["op"] = "add",
            ["section"] = Section(a.Section),
            ["id"] = a.Id.ToString(),
            ["afterId"] = a.AfterId?.ToString(),
            ["blocks"] = WriteBlocks(a.Blocks),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private static string Section(StepSection s) => s == StepSection.Prep ? "prep" : "instructions";

    private static JsonArray WriteSteps(IReadOnlyList<ResolvedStep> steps) =>
        new([.. steps.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id.ToString(), ["blocks"] = WriteBlocks(s.Blocks) })]);

    private static JsonArray WriteBlocks(IReadOnlyList<ContentBlock> blocks) =>
        new([.. blocks.Select(b => (JsonNode)(b switch
        {
            TextBlock t => new JsonObject { ["type"] = "text", ["text"] = t.Text },
            ImageBlock i => new JsonObject { ["type"] = "image", ["imageId"] = i.ImageId.ToString(), ["caption"] = i.Caption },
            _ => throw new ArgumentOutOfRangeException(nameof(blocks)),
        }))]);

    private static JsonObject? WriteQuantity(Quantity? q) => q is null
        ? null
        : new JsonObject
        {
            ["value"] = q.Value,
            ["unit"] = JsonNamingPolicy.CamelCase.ConvertName(q.Unit.ToString()),
            ["size"] = q.Size is { } size ? JsonNamingPolicy.CamelCase.ConvertName(size.ToString()) : null,
        };

    private static void AddSteps(Recipe recipe, JsonElement steps, StepSection section)
    {
        var position = 0;
        foreach (var s in steps.EnumerateArray())
        {
            recipe.Steps.Add(new RecipeStep
            {
                Id = s.GetProperty("id").GetGuid(),
                RecipeId = recipe.Id,
                Section = section,
                Position = position++,
                Blocks = [.. ReadBlocks(s.GetProperty("blocks"))],
            });
        }
    }

    private static List<StepDraft> ReadStepDrafts(JsonElement steps) =>
        [.. steps.EnumerateArray().Select(s => new StepDraft(
            s.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetGuid() : null,
            ReadBlocks(s.GetProperty("blocks"))))];

    private static IngredientOp ReadIngredientOp(JsonElement op) => op.GetProperty("op").GetString() switch
    {
        "modify" => new ModifyIngredientOp(
            op.GetProperty("targetId").GetGuid(),
            op.GetProperty("productId").GetGuid(),
            ReadQuantity(op.GetProperty("quantity")),
            OptionalDecimal(op.GetProperty("baseAmount")),
            OptionalString(op.GetProperty("note")),
            op.GetProperty("optional").GetBoolean()),
        "remove" => new RemoveIngredientOp(op.GetProperty("targetId").GetGuid()),
        "add" => new AddIngredientOp(
            op.GetProperty("id").GetGuid(),
            op.GetProperty("afterId").ValueKind == JsonValueKind.Null ? null : op.GetProperty("afterId").GetGuid(),
            op.GetProperty("productId").GetGuid(),
            ReadQuantity(op.GetProperty("quantity")),
            OptionalDecimal(op.GetProperty("baseAmount")),
            OptionalString(op.GetProperty("note")),
            op.GetProperty("optional").GetBoolean()),
        var other => throw new InvalidDataException($"unknown ingredient op '{other}'"),
    };

    private static StepOp ReadStepOp(JsonElement op)
    {
        var section = op.GetProperty("section").GetString() == "prep" ? StepSection.Prep : StepSection.Instructions;
        return op.GetProperty("op").GetString() switch
        {
            "replace" => new ReplaceStepOp(section, op.GetProperty("targetId").GetGuid(), ReadBlocks(op.GetProperty("blocks"))),
            "remove" => new RemoveStepOp(section, op.GetProperty("targetId").GetGuid()),
            "add" => new AddStepOp(
                section,
                op.GetProperty("id").GetGuid(),
                op.GetProperty("afterId").ValueKind == JsonValueKind.Null ? null : op.GetProperty("afterId").GetGuid(),
                ReadBlocks(op.GetProperty("blocks"))),
            var other => throw new InvalidDataException($"unknown step op '{other}'"),
        };
    }

    private static List<ContentBlock> ReadBlocks(JsonElement blocks) =>
        [.. blocks.EnumerateArray().Select(b => b.GetProperty("type").GetString() switch
        {
            "text" => (ContentBlock)new TextBlock(b.GetProperty("text").GetString()!),
            "image" => new ImageBlock(
                b.GetProperty("imageId").GetGuid(),
                b.TryGetProperty("caption", out var caption) ? OptionalString(caption) : null),
            var other => throw new InvalidDataException($"unknown block type '{other}'"),
        })];

    private static Quantity? ReadQuantity(JsonElement q)
    {
        if (q.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var size = q.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.String
            ? Enum.Parse<PieceSize>(s.GetString()!, ignoreCase: true)
            : (PieceSize?)null;
        return new Quantity(q.GetProperty("value").GetDecimal(), Enum.Parse<Unit>(q.GetProperty("unit").GetString()!, ignoreCase: true), size);
    }

    private static int? OptionalInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    private static decimal? OptionalDecimal(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetDecimal();

    private static string? OptionalString(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString();
}
