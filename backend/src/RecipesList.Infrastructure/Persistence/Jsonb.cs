using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RecipesList.Infrastructure.Persistence;

/// <summary>
/// Maps a CLR value to a jsonb column through System.Text.Json, in the same camelCase shape as the API, so raw SQL
/// (e.g. re-pointing <c>productId</c> in variant ops, finding <c>imageId</c> references) sees API field names.
/// </summary>
public static class Jsonb
{
    /// <summary>The serializer options for every jsonb column.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Configures the property as jsonb with a converter and a snapshot comparer.</summary>
    public static PropertyBuilder<T> HasJsonbConversion<T>(this PropertyBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var converter = new ValueConverter<T, string>(
            v => Serialize(v),
            s => Deserialize<T>(s));

        var comparer = new ValueComparer<T>(
            (a, b) => Serialize(a) == Serialize(b),
            v => Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => Deserialize<T>(Serialize(v)));

        builder.HasConversion(converter, comparer).HasColumnType("jsonb");
        return builder;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Unexpected null jsonb value.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // PostgreSQL reorders jsonb keys, so the polymorphic discriminator ("type"/"op") is not always first.
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
