using RecipesList.Core.Common;
using RecipesList.Core.Users;

namespace RecipesList.UnitTests.Users;

/// <summary>PROJECT §2: 3–30 characters, letters (Polish included), digits, <c>_</c> and <c>-</c>; unique case-insensitively.</summary>
public sealed class NicknameRulesTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("Ania")]
    [InlineData("ania_k-1")]
    [InlineData("123")]
    [InlineData("___")]
    [InlineData("Łucja")]
    [InlineData("zażółć-gęślą-jaźń")]
    [InlineData("ĄĆĘŁŃÓŚŹŻ")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234")]
    [InlineData("  ania  ")]
    public void IsValid_AllowedNicknames_ReturnsTrue(string nickname) => NicknameRules.IsValid(nickname).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData(" ab ")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345")]
    [InlineData("ania kowalska")]
    [InlineData("ania.k")]
    [InlineData("ania@x")]
    [InlineData("ania!")]
    [InlineData("an\tia")]
    [InlineData("ania😀")]
    [InlineData("<script>")]
    [InlineData(null)]
    public void IsValid_ForbiddenNicknames_ReturnsFalse(string? nickname) => NicknameRules.IsValid(nickname).ShouldBeFalse();

    [Fact]
    public void Validate_Valid_ReturnsTheTrimmedNickname() => NicknameRules.Validate("  Żaneta ").ShouldBe("Żaneta");

    [Fact]
    public void Validate_Invalid_ThrowsNicknameInvalidOnTheNicknameField()
    {
        var ex = Should.Throw<DomainValidationException>(() => NicknameRules.Validate("a b"));

        ex.Code.ShouldBe(ErrorCodes.NicknameInvalid);
        ex.Errors["nickname"].ShouldBe([ErrorCodes.NicknameInvalid]);
    }

    [Fact]
    public void Validate_DecomposedPolishLetters_AreComposed()
    {
        var decomposed = "Zażółć".Normalize(System.Text.NormalizationForm.FormD);

        NicknameRules.Validate(decomposed).ShouldBe("Zażółć".Normalize(System.Text.NormalizationForm.FormC));
    }

    [Theory]
    [InlineData("Ania", "ania")]
    [InlineData("ANIA", "ania")]
    [InlineData("Łucja", "lucja")]
    [InlineData("Żaneta_1", "zaneta_1")]
    public void Normalize_IgnoresCaseAndDiacritics(string nickname, string expected) =>
        NicknameRules.Normalize(nickname).ShouldBe(expected);
}
