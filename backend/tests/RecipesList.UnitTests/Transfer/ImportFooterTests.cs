using RecipesList.Core.Transfer;

namespace RecipesList.UnitTests.Transfer;

public sealed class ImportFooterTests
{
    [Fact]
    public void Append_ToDescription_AddsTheFooterAfterABlankLine()
    {
        ImportFooter.Append("Prosty obiad", "ania", 2000).ShouldBe("Prosty obiad\n\nZaimportowano — oryginalnie: @ania");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Append_WithoutDescription_IsJustTheFooter(string? description)
    {
        ImportFooter.Append(description, "ania", 2000).ShouldBe("Zaimportowano — oryginalnie: @ania");
    }

    [Fact]
    public void Append_ToALongDescription_CutsTheDescriptionToFit()
    {
        var result = ImportFooter.Append(new string('a', 500), "ania", 100);

        result.Length.ShouldBeLessThanOrEqualTo(100);
        result.ShouldEndWith("…\n\nZaimportowano — oryginalnie: @ania");
    }

    [Theory]
    [InlineData("Prosty obiad")]
    [InlineData("Dwa\n\nakapity")]
    [InlineData(null)]
    public void Strip_UndoesAppend(string? description)
    {
        var (text, nickname) = ImportFooter.Strip(ImportFooter.Append(description, "ania_k", 2000));

        text.ShouldBe(description);
        nickname.ShouldBe("ania_k");
    }

    [Theory]
    [InlineData("Bez stopki")]
    [InlineData("Zaimportowano — oryginalnie: @ania w środku zdania")]
    [InlineData("Tekst Zaimportowano — oryginalnie: @ania")]
    [InlineData("Zaimportowano — oryginalnie: @")]
    public void Strip_WithoutATrailingFooter_ReturnsTheDescriptionUntouched(string description)
    {
        var (text, nickname) = ImportFooter.Strip(description);

        text.ShouldBe(description);
        nickname.ShouldBeNull();
    }
}
