using System.Text.Json;

namespace RecipesList.UnitTests;

/// <summary>
/// Loader for <c>shared/test-vectors/&lt;kind&gt;/*.json</c> (TECHNICAL §6). Every file of the directory is read,
/// in name order; a case id that appears twice fails the load.
/// </summary>
public static class TestVectors
{
    private static readonly Lazy<string> Root = new(FindRoot);

    /// <summary>The <c>shared/test-vectors</c> directory of this checkout.</summary>
    public static string Directory => Root.Value;

    /// <summary>All cases of one kind (e.g. <c>quantities</c>), keyed by their id.</summary>
    public static IReadOnlyDictionary<string, JsonElement> Load(string kind) => LoadFrom(Path.Combine(Directory, kind));

    /// <summary>All cases of every <c>*.json</c> file in <paramref name="directory"/>.</summary>
    public static IReadOnlyDictionary<string, JsonElement> LoadFrom(string directory)
    {
        var cases = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in System.IO.Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("cases", out var list)
                || list.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"{name}: expected an object with a \"cases\" array");
            }

            foreach (var item in list.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidDataException($"{name}: a case has no \"id\"");
                }

                if (sources.TryGetValue(id, out var previous))
                {
                    throw new InvalidDataException($"Duplicate test-vector id \"{id}\" in {previous} and {name}");
                }

                sources[id] = name;
                cases[id] = item.Clone();
            }
        }

        return cases;
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "shared", "test-vectors");
            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("shared/test-vectors not found above " + AppContext.BaseDirectory);
    }
}
