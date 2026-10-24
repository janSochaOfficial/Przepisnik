using System.Text.Json.Serialization;
using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Units;

namespace RecipesList.Api.Features.Units;

// Request and response shapes of API.md §2.1, plus the response form of Quantity that every feature embeds (API.md §0).

/// <summary>A custom unit (API.md §0 <c>CustomUnitRef</c>): "1 <c>name</c>" = <c>amount</c> base units of <c>measureKind</c>.</summary>
public sealed record CustomUnitRefDto(Guid Id, string Name, PluralForms Forms, Guid? ProductId, MeasureKind MeasureKind, decimal Amount)
{
    public static CustomUnitRefDto From(CustomUnit u)
    {
        ArgumentNullException.ThrowIfNull(u);
        return new(u.Id, u.Name, u.Forms, u.ProductId, u.MeasureKind, u.Amount);
    }
}

/// <summary>
/// A quantity in a response (API.md §0 <c>Quantity</c>): as entered, with the custom unit embedded when <c>unit</c> is
/// <c>custom</c> (so clients never need a second request to show it).
/// </summary>
public sealed record QuantityDto(decimal Value, Unit Unit, PieceSize? Size, Guid? CustomUnitId, CustomUnitRefDto? CustomUnit)
{
    /// <summary>Null for "do smaku"; the unit is looked up in <paramref name="units"/> (loaded in one query).</summary>
    public static QuantityDto? From(Quantity? q, IReadOnlyDictionary<Guid, CustomUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        return q is null ? null : From(q, units.UnitOf(q));
    }

    /// <summary>Null for "do smaku".</summary>
    public static QuantityDto? From(Quantity? q, CustomUnit? unit) => q is null
        ? null
        : new QuantityDto(q.Value, q.Unit, q.Size, q.CustomUnitId, q.Unit == Unit.Custom && unit is not null ? CustomUnitRefDto.From(unit) : null);
}

/// <summary>The built-in quantity one unit equals (API.md §2.1 <c>CustomUnitWrite.equals</c>).</summary>
public sealed record CustomUnitEqualsRequest
{
    [JsonRequired]
    public decimal Value { get; init; }

    [JsonRequired]
    public Unit Unit { get; init; }

    public PieceSize? Size { get; init; }
}

/// <summary>Body of <c>POST /units</c> and <c>PUT /admin/units/{id}</c> (API.md §2.1 <c>CustomUnitWrite</c>).</summary>
public sealed record CustomUnitWriteRequest
{
    public required string Name { get; init; }

    public required PluralForms Forms { get; init; }

    /// <summary>Null for a unit for every product.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>JSON <c>equals</c> (a C# member cannot be called <c>Equals</c>).</summary>
    [JsonPropertyName("equals")]
    public required CustomUnitEqualsRequest EqualTo { get; init; }

    public CustomUnitInput ToInput() => new(Name, Forms, ProductId, new Quantity(EqualTo.Value, EqualTo.Unit, EqualTo.Size));
}
