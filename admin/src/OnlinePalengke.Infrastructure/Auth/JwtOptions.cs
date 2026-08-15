using System.ComponentModel.DataAnnotations;

namespace OnlinePalengke.Infrastructure.Auth;

/// <summary>JWT signing configuration, bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signing secret. Never in appsettings — local dev sets it via
    /// <c>dotnet user-secrets</c>, production reads it from the server's <c>.env</c>.
    /// Must be at least 32 bytes once UTF-8 encoded (HS256's minimum key size);
    /// startup validation rejects anything shorter rather than silently accepting
    /// a weak key.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;
}
