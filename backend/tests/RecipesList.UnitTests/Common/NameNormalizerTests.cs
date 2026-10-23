using RecipesList.Core.Common;

namespace RecipesList.UnitTests.Common;

public sealed class NameNormalizerTests
{
    [Theory]
    [InlineData("cebula", "cebula")]
    [InlineData("  Cebula ", "cebula")]
    [InlineData("CEBULA", "cebula")]
    [InlineData("Śmietana 18%", "smietana 18%")]
    [InlineData("śmietanka   30%", "smietanka 30%")]
    [InlineData("Żółć", "zolc")]
    [InlineData("ŁOSOŚ wędzony", "losos wedzony")]
    [InlineData("pierś\tz  kurczaka", "piers z kurczaka")]
    [InlineData("ąćęłńóśźż", "acelnoszz")]
    [InlineData("Ania_92", "ania_92")]
    [InlineData("crème brûlée", "creme brulee")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_Input_ReturnsLowerCaseWithoutDiacriticsAndExtraSpaces(string input, string expected)
    {
        NameNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_DecomposedInput_MatchesComposedInput()
    {
        var composed = "zażółć";
        var decomposed = composed.Normalize(System.Text.NormalizationForm.FormD);

        NameNormalizer.Normalize(decomposed).ShouldBe(NameNormalizer.Normalize(composed));
    }

    [Fact]
    public void Normalize_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => NameNormalizer.Normalize(null!));
    }
}
