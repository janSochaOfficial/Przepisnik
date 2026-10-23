using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>
/// Validates JSON bodies against the schemas of <c>docs/api/openapi.yaml</c> (the normative contract, TECHNICAL §5).
/// The OpenAPI 3.1 component schemas are JSON Schema 2020-12, so they are evaluated directly with JsonSchema.Net.
/// </summary>
public static partial class ContractAssert
{
    private static readonly Lazy<JsonObject> Components = new(LoadComponentSchemas);
    private static readonly ConcurrentDictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);

    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    /// <summary>The names of every schema under <c>components.schemas</c>.</summary>
    public static IReadOnlyCollection<string> SchemaNames => [.. Components.Value.Select(kv => kv.Key)];

    /// <summary>Fails when <paramref name="body"/> does not match <c>#/components/schemas/&lt;schemaName&gt;</c>.</summary>
    public static void MatchesSchema(JsonElement body, string schemaName)
    {
        var errors = Validate(body, schemaName);
        errors.ShouldBeEmpty($"Body does not match contract schema '{schemaName}':\n{string.Join('\n', errors)}\n{body}");
    }

    /// <summary>Returns the validation errors of <paramref name="body"/> against a contract schema (empty when valid).</summary>
    public static IReadOnlyList<string> Validate(JsonElement body, string schemaName)
    {
        var schema = Schemas.GetOrAdd(schemaName, Build);
        var result = schema.Evaluate(body, Options);
        if (result.IsValid)
        {
            return [];
        }

        return [.. (result.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation} ({d.EvaluationPath}): {e.Key} {e.Value}"))
            .DefaultIfEmpty("(invalid)")];
    }

    /// <summary>Asserts the status, the problem+json content type, the contract shape and the error <c>code</c>.</summary>
    public static async Task<JsonElement> IsProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(status, body.ToString());
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        MatchesSchema(body, "ProblemDetails");
        body.GetProperty("code").GetString().ShouldBe(code);
        return body;
    }

    /// <summary>Asserts a JSON success response of <paramref name="status"/> whose body matches the schema.</summary>
    public static async Task<JsonElement> IsJsonAsync(HttpResponseMessage response, HttpStatusCode status, string schemaName)
    {
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(status, body.ToString());
        MatchesSchema(body, schemaName);
        return body;
    }

    private static JsonSchema Build(string schemaName)
    {
        if (!Components.Value.ContainsKey(schemaName))
        {
            throw new ArgumentException($"openapi.yaml has no schema named '{schemaName}'.", nameof(schemaName));
        }

        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$ref"] = $"#/$defs/{schemaName}",
            ["$defs"] = Components.Value.DeepClone(),
        };
        var buildOptions = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        return JsonSchema.FromText(root.ToJsonString(), buildOptions, new Uri($"https://recipes-list.local/contract/{schemaName}"));
    }

    private static JsonObject LoadComponentSchemas()
    {
        var path = FindContract();
        var yaml = new YamlStream();
        using (var reader = new StreamReader(path, Encoding.UTF8))
        {
            yaml.Load(reader);
        }

        var document = ToJson(yaml.Documents[0].RootNode) as JsonObject
            ?? throw new InvalidDataException("openapi.yaml is not an object");
        var schemas = document["components"]?["schemas"] as JsonObject
            ?? throw new InvalidDataException("openapi.yaml has no components.schemas");

        // Component references become local $defs references.
        var text = schemas.ToJsonString().Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal);
        return JsonNode.Parse(text)!.AsObject();
    }

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode map => new JsonObject(map.Children.Select(kv =>
            KeyValuePair.Create(((YamlScalarNode)kv.Key).Value ?? "", ToJson(kv.Value)))),
        YamlSequenceNode seq => new JsonArray([.. seq.Children.Select(ToJson)]),
        YamlScalarNode scalar => Scalar(scalar),
        _ => throw new InvalidDataException($"Unsupported YAML node {node.NodeType}"),
    };

    // Plain scalars follow the YAML core schema (null, booleans, numbers); quoted scalars are always strings.
    private static JsonValue? Scalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? "";
        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or ScalarStyle.Literal or ScalarStyle.Folded)
        {
            return JsonValue.Create(value);
        }

        return value switch
        {
            "" or "~" or "null" => null,
            "true" => JsonValue.Create(true),
            "false" => JsonValue.Create(false),
            _ when IntegerPattern().IsMatch(value) => JsonValue.Create(long.Parse(value, CultureInfo.InvariantCulture)),
            _ when NumberPattern().IsMatch(value) => JsonValue.Create(decimal.Parse(value, CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value),
        };
    }

    private static string FindContract()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "api", "openapi.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("docs/api/openapi.yaml not found above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^-?\d+\.\d+([eE][-+]?\d+)?$")]
    private static partial Regex NumberPattern();
}
