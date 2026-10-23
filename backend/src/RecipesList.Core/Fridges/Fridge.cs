using RecipesList.Core.Users;

namespace RecipesList.Core.Fridges;

/// <summary>A member's role in a fridge (PROJECT §8.1).</summary>
public enum FridgeRole
{
    Owner,
    Member,
}

/// <summary>A (possibly shared) fridge (PROJECT §8).</summary>
public sealed class Fridge
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public Guid OwnerId { get; set; }

    public User? Owner { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<FridgeMember> Members { get; set; } = [];
}

/// <summary>Membership of a user in a fridge. The owner is a member with <see cref="FridgeRole.Owner"/>.</summary>
public sealed class FridgeMember
{
    public Guid FridgeId { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public FridgeRole Role { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}

/// <summary>A reusable invite link, valid for 7 days unless revoked (D-08).</summary>
public sealed class FridgeInvite
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FridgeId { get; set; }

    /// <summary>URL-safe random token; unique.</summary>
    public required string Token { get; set; }

    public Guid CreatedById { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
