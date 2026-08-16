namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// A delivery rider's onboarding profile, one-to-one with a <see cref="UserRole.Rider"/>
/// user. Mirrors <see cref="Partner"/>'s shape and KYC lifecycle deliberately — a rider has
/// no category, so every rider document type applies role-wide.
/// </summary>
/// <remarks>
/// <see cref="Status"/> starts at <see cref="RiderStatus.PendingKyc"/> and is otherwise
/// entirely derived by <c>KycDocumentService</c> (Application layer, Epic 4 task #36) —
/// see the identical note on <see cref="Partner.Status"/>.
/// </remarks>
public sealed class Rider
{
    public long Id { get; init; }

    public required long UserId { get; init; }

    public required string FullName { get; init; }

    public string? VehicleType { get; init; }

    public required RiderStatus Status { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
