using RecipesList.Core.Fridges;

namespace RecipesList.Core.Users;

/// <summary>How liquid amounts between 62.5 and 1000 ml are shown (PROJECT §5.4).</summary>
public enum LiquidUnits
{
    Kitchen,
    Metric,
}

/// <summary>A person signed in with Google (PROJECT §2).</summary>
public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Google <c>sub</c>; unique. Dev-login users get a <c>dev|</c> prefixed value.</summary>
    public required string GoogleSubject { get; set; }

    /// <summary>Never exposed to other users.</summary>
    public required string Email { get; set; }

    /// <summary>Null until onboarding is finished.</summary>
    public string? Nickname { get; set; }

    /// <summary><see cref="Common.NameNormalizer"/> form of <see cref="Nickname"/>; unique.</summary>
    public string? NormalizedNickname { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsAdmin { get; set; }

    public LiquidUnits LiquidUnits { get; set; } = LiquidUnits.Kitchen;

    public Guid? DefaultFridgeId { get; set; }

    public Fridge? DefaultFridge { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
