using RecipesList.Api.Configuration;

namespace RecipesList.IntegrationTests.Infrastructure;

public sealed class DotEnvLocalTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("dotenv-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void Parse_Lines_ReadsKeyValuePairs()
    {
        var pairs = DotEnvLocal.Parse(["# ports", "", "API_PORT=5100", "export WEB_PORT = 5200", "PREVIEW_PORT=\"5300\"", "broken"]);

        pairs.ShouldBe([("API_PORT", "5100"), ("WEB_PORT", "5200"), ("PREVIEW_PORT", "5300")]);
    }

    [Fact]
    public void Find_NestedDirectory_StopsAtTheRepositoryRoot()
    {
        var repo = _root.CreateSubdirectory("repo");
        File.WriteAllText(Path.Combine(_root.FullName, DotEnvLocal.FileName), "API_PORT=1");
        File.WriteAllText(Path.Combine(repo.FullName, ".git"), "gitdir: elsewhere");
        var nested = repo.CreateSubdirectory("backend").CreateSubdirectory("src");

        DotEnvLocal.Find(nested.FullName).ShouldBeNull();

        File.WriteAllText(Path.Combine(repo.FullName, DotEnvLocal.FileName), "API_PORT=2");
        DotEnvLocal.Find(nested.FullName).ShouldBe(Path.Combine(repo.FullName, DotEnvLocal.FileName));
    }
}
