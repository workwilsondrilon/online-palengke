using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Auth;

/// <summary>
/// Drives the phone-OTP flow for the three mobile apps.
/// </summary>
/// <remarks>
/// Routed by role at the API layer (<c>/api/{role}/auth/otp/...</c>), not a
/// single global endpoint — the schema gives every <c>users</c> row exactly
/// one role, so the server needs to know at OTP-request time which role a
/// brand-new phone number should register as. There is no field for this in
/// the request body precisely because the URL already carries it.
/// </remarks>
public sealed class OtpAuthService(
    IOtpCodeRepository otpCodes,
    IUserRepository users,
    IPasswordHasher hasher,
    ISmsSender sms,
    AuthSessionFactory sessionFactory,
    IRefreshTokenRepository refreshTokens,
    IClock clock,
    ILogger<OtpAuthService> logger)
{
    /// <summary>Minimum gap between two OTP requests for the same phone.</summary>
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Requests issued per phone in this window before further requests are refused.</summary>
    private const int MaxRequestsPerWindow = 5;

    private static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);

    public async Task<RequestOtpResponse> RequestOtpAsync(
        UserRole role,
        string rawPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        var phone = NormalizeOrThrow(rawPhoneNumber);
        var now = clock.UtcNow;

        var latest = await otpCodes.GetLatestForPhoneAsync(phone, cancellationToken);
        if (latest is not null)
        {
            var sinceLast = now - latest.CreatedAtUtc;
            if (sinceLast < ResendCooldown)
            {
                throw new TooManyRequestsException(
                    "Please wait before requesting another code.", ResendCooldown - sinceLast);
            }
        }

        var recentCount = await otpCodes.CountIssuedSinceAsync(phone, now - RequestWindow, cancellationToken);
        if (recentCount >= MaxRequestsPerWindow)
        {
            throw new TooManyRequestsException(
                "Too many codes requested for this number recently. Try again later.", RequestWindow);
        }

        // A helpful, specific refusal rather than a mysterious failure: these are three
        // complementary first-party apps, not competing accounts, so confirming which
        // app a number already belongs to is a UX kindness, not an information leak.
        var existingUser = await users.GetByPhoneAsync(phone, cancellationToken);
        if (existingUser is not null && existingUser.Role != role)
        {
            throw new ValidationException(
                "phoneNumber",
                $"This number is already registered as a {Naming.ToDbValue(existingUser.Role)}. " +
                $"Use that app to sign in.");
        }

        var code = GenerateSixDigitCode();

        var otpId = await otpCodes.InsertAsync(
            new OtpCode
            {
                Phone = phone,
                CodeHash = hasher.Hash(code),
                ExpiresAtUtc = now.Add(OtpCode.Lifetime),
                CreatedAtUtc = now,
            },
            cancellationToken);

        await sms.SendOtpAsync(phone, code, cancellationToken);

        logger.LogInformation("Issued OTP {OtpId} for {Phone} ({Role})", otpId, phone, role);

        return new RequestOtpResponse(
            ChallengeId: null,
            CodeLength: 6,
            ExpiresInSeconds: (int)OtpCode.Lifetime.TotalSeconds,
            ResendAfterSeconds: (int)ResendCooldown.TotalSeconds);
    }

    public async Task<AuthSessionResponse> VerifyOtpAsync(
        UserRole role,
        string rawPhoneNumber,
        string submittedCode,
        CancellationToken cancellationToken = default)
    {
        var phone = NormalizeOrThrow(rawPhoneNumber);

        if (string.IsNullOrWhiteSpace(submittedCode))
        {
            throw new ValidationException("code", "Enter the code we sent you.");
        }

        var now = clock.UtcNow;
        var otp = await otpCodes.GetLatestForPhoneAsync(phone, cancellationToken);

        if (otp is null || !otp.CanAttempt(now))
        {
            throw new ValidationException("code", "This code is invalid or has expired. Request a new one.");
        }

        if (!hasher.Verify(submittedCode.Trim(), otp.CodeHash))
        {
            otp.RecordFailedAttempt();
            await otpCodes.RecordFailedAttemptAsync(otp.Id, otp.AttemptCount, cancellationToken);
            throw new ValidationException("code", "That code is incorrect.");
        }

        await otpCodes.MarkConsumedAsync(otp.Id, now, cancellationToken);

        var user = await users.GetByPhoneAsync(phone, cancellationToken);
        if (user is not null && user.Role != role)
        {
            // Someone else claimed this phone under a different role between request and
            // verify — vanishingly rare, but the response must not silently issue a
            // session under the wrong role.
            throw new ValidationException(
                "phoneNumber",
                $"This number is already registered as a {Naming.ToDbValue(user.Role)}. Use that app to sign in.");
        }

        if (user is null)
        {
            var newUserId = await users.InsertPhoneUserAsync(phone, role, cancellationToken);
            user = await users.GetByIdAsync(newUserId, cancellationToken)
                ?? throw new InvalidOperationException("Newly inserted user could not be re-read.");
        }

        var (access, refreshPlain) = await sessionFactory.IssueAsync(user, cancellationToken);

        return new AuthSessionResponse(
            access.Value,
            refreshPlain,
            access.ExpiresAtUtc,
            new AuthUserResponse(user.Id, user.Phone ?? phone, DisplayName: null, Roles: [Naming.ToDbValue(user.Role)]));
    }

    public async Task<AuthSessionResponse> RefreshAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
        {
            throw new UnauthorizedAppException("Session expired. Please sign in again.");
        }

        var now = clock.UtcNow;
        var hash = AuthSessionFactory.HashToken(refreshTokenPlain);
        var existing = await refreshTokens.GetByTokenHashAsync(hash, cancellationToken);

        if (existing is null || !existing.IsUsable(now))
        {
            throw new UnauthorizedAppException("Session expired. Please sign in again.");
        }

        var user = await users.GetByIdAsync(existing.UserId, cancellationToken)
            ?? throw new UnauthorizedAppException("Session expired. Please sign in again.");

        if (!user.CanAuthenticate)
        {
            throw new UnauthorizedAppException("This account is no longer active.");
        }

        // Rotate: the presented token is spent regardless of what happens next, so a
        // replayed old token is a detectable signal rather than a silently reusable one.
        await refreshTokens.RevokeAsync(existing.Id, now, cancellationToken);

        var (access, newRefreshPlain) = await sessionFactory.IssueAsync(user, cancellationToken);

        return new AuthSessionResponse(
            access.Value,
            newRefreshPlain,
            access.ExpiresAtUtc,
            new AuthUserResponse(user.Id, user.Phone ?? string.Empty, DisplayName: null, Roles: [Naming.ToDbValue(user.Role)]));
    }

    public async Task SignOutAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
        {
            return;
        }

        var hash = AuthSessionFactory.HashToken(refreshTokenPlain);
        var existing = await refreshTokens.GetByTokenHashAsync(hash, cancellationToken);

        // Signing out an already-signed-out session is not an error - the caller's goal
        // (end up signed out) is already true.
        if (existing is not null && !existing.IsRevoked)
        {
            await refreshTokens.RevokeAsync(existing.Id, clock.UtcNow, cancellationToken);
        }
    }

    private static string NormalizeOrThrow(string rawPhoneNumber)
    {
        return PhilippineMobileNumber.Normalize(rawPhoneNumber)
            ?? throw new ValidationException("phoneNumber", "Enter a valid Philippine mobile number.");
    }

    /// <summary>Cryptographically random, uniform over 000000-999999 (leading zeros kept).</summary>
    private static string GenerateSixDigitCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
