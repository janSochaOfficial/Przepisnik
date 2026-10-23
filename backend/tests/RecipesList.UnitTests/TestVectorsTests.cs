namespace RecipesList.UnitTests;

public sealed class TestVectorsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vectors-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void LoadFrom_SeveralFiles_ReadsEveryJsonFile()
    {
        File.WriteAllText(Path.Combine(_dir, "A.json"), """{ "cases": [ { "id": "A-1" } ] }""");
        File.WriteAllText(Path.Combine(_dir, "B.json"), """{ "cases": [ { "id": "B-1" }, { "id": "B-2" } ] }""");
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "ignored");

        TestVectors.LoadFrom(_dir).Keys.ShouldBe(["A-1", "B-1", "B-2"], ignoreOrder: true);
    }

    [Fact]
    public void LoadFrom_DuplicateIdAcrossFiles_Throws()
    {
        File.WriteAllText(Path.Combine(_dir, "A.json"), """{ "cases": [ { "id": "X-1" } ] }""");
        File.WriteAllText(Path.Combine(_dir, "B.json"), """{ "cases": [ { "id": "X-1" } ] }""");

        var error = Should.Throw<InvalidDataException>(() => TestVectors.LoadFrom(_dir));
        error.Message.ShouldContain("Duplicate test-vector id \"X-1\" in A.json and B.json");
    }

    [Fact]
    public void LoadFrom_FileWithoutCases_Throws()
    {
        File.WriteAllText(Path.Combine(_dir, "A.json"), """[ { "id": "A-1" } ]""");

        Should.Throw<InvalidDataException>(() => TestVectors.LoadFrom(_dir));
    }

    [Theory]
    [InlineData("quantities", 25)]
    [InlineData("formatting", 30)]
    [InlineData("variants", 5)]
    [InlineData("matching", 0)]
    public void Load_SharedKinds_HaveUniqueIdsAndEnoughCases(string kind, int minimum)
    {
        TestVectors.Load(kind).Count.ShouldBeGreaterThanOrEqualTo(minimum);
    }
}
