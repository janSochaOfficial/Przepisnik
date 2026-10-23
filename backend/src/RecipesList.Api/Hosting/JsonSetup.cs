using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipesList.Api.Hosting;

/// <summary>HTTP JSON options (TECHNICAL §3.3).</summary>
internal static class JsonSetup
{
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null; // slot keys and error paths are sent verbatim
        options.PropertyNameCaseInsensitive = false;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.AllowDuplicateProperties = false;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.AllowOutOfOrderMetadataProperties = true;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }
}
