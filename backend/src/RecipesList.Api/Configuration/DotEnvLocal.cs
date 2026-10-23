namespace RecipesList.Api.Configuration;

/// <summary>
/// Loads the worktree's git-ignored <c>.env.local</c> (per-agent ports, plan §4.3) into the process environment.
/// Variables that are already set win, so CI, Docker and explicit exports are never overridden.
/// </summary>
internal static class DotEnvLocal
{
    public const string FileName = ".env.local";

    /// <summary>Walks up from <paramref name="startDirectory"/> to the repository root and loads the first file found.</summary>
    public static string? Load(string startDirectory)
    {
        var path = Find(startDirectory);
        if (path is null)
        {
            return null;
        }

        foreach (var (key, value) in Parse(File.ReadAllLines(path)))
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        return path;
    }

    internal static string? Find(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Stop at the repository (or worktree) root so a nested worktree never reads its parent's file.
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return null;
            }
        }

        return null;
    }

    internal static IEnumerable<(string Key, string Value)> Parse(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"', '\'');
            yield return (key, value);
        }
    }
}
