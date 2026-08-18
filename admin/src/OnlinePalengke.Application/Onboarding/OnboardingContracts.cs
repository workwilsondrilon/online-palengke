namespace OnlinePalengke.Application.Onboarding;

public sealed record RegisterPartnerRequest(string StallName, long? MarketId, long? CategoryId);

public sealed record PartnerResponse(
    long Id,
    long UserId,
    string StallName,
    long? MarketId,
    long? CategoryId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record RegisterRiderRequest(string FullName, string? VehicleType);

public sealed record RiderResponse(
    long Id,
    long UserId,
    string FullName,
    string? VehicleType,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
