namespace OnlinePalengke.Domain.Catalog;

/// <summary>A unit of measure a shopping-list line or quote line can be priced in — kg, piece, bundle, tali.</summary>
public sealed class Unit
{
    public long Id { get; init; }

    /// <summary>Short machine code, e.g. "kg", "pc", "bundle". Stable — referenced by items and quote lines.</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }

    /// <summary>Whether a quantity like 1.5 is meaningful for this unit (true for "kg", false for "piece").</summary>
    public required bool AllowsFractionalQuantity { get; init; }

    public required bool IsActive { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
