using System.Text.Json;

namespace RecipesList.IntegrationTests.Infrastructure;

public sealed class ContractAssertTests
{
    [Fact]
    public void SchemaNames_Contract_ContainsTheApiTypes()
    {
        ContractAssert.SchemaNames.ShouldContain("Me");
        ContractAssert.SchemaNames.ShouldContain("RecipeDetail");
        ContractAssert.SchemaNames.ShouldContain("ProblemDetails");
    }

    [Fact]
    public void Validate_ValidBody_HasNoErrors()
    {
        var body = Parse("""
            { "id": "01a16a25-164b-79a4-8930-f212112200de", "email": "a@test.local", "nickname": null, "avatarUrl": null,
              "isAdmin": false, "settings": { "liquidUnits": "kitchen", "defaultFridgeId": null } }
            """);

        ContractAssert.Validate(body, "Me").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "id": "x", "email": "a", "nickname": null, "avatarUrl": null, "isAdmin": false, "settings": { "liquidUnits": "kitchen" } }""")]
    [InlineData("""{ "id": "01a16a25-164b-79a4-8930-f212112200de", "email": "a", "avatarUrl": null, "isAdmin": false, "settings": { "liquidUnits": "kitchen" } }""")]
    [InlineData("""{ "id": "01a16a25-164b-79a4-8930-f212112200de", "email": "a", "nickname": null, "avatarUrl": null, "isAdmin": false, "settings": { "liquidUnits": "cups" } }""")]
    [InlineData("""{ "id": "01a16a25-164b-79a4-8930-f212112200de", "email": "a", "nickname": null, "avatarUrl": null, "isAdmin": false, "settings": { "liquidUnits": "kitchen" }, "extra": 1 }""")]
    public void Validate_InvalidBody_ReportsErrors(string json)
    {
        ContractAssert.Validate(Parse(json), "Me").ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_PolymorphicBlocks_UseTheDiscriminatedSchemas()
    {
        ContractAssert.Validate(Parse("""{ "type": "text", "text": "a" }"""), "ContentBlock").ShouldBeEmpty();
        ContractAssert.Validate(Parse("""{ "type": "image", "imageId": "01a16a25-164b-79a4-8930-f212112200de", "caption": null }"""), "ContentBlock").ShouldBeEmpty();
        ContractAssert.Validate(Parse("""{ "type": "video", "text": "a" }"""), "ContentBlock").ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_UnknownSchema_Throws()
    {
        Should.Throw<ArgumentException>(() => ContractAssert.Validate(Parse("{}"), "NoSuchSchema"));
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
