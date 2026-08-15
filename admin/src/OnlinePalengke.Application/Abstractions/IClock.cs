namespace OnlinePalengke.Application.Abstractions;

/// <summary>
/// The current time, injected rather than read from <c>DateTime.UtcNow</c>.
/// </summary>
/// <remarks>
/// Not ceremony — this codebase is full of deadlines. Quote windows close, quotes
/// expire, unpaid selections lapse at a cutoff, and KYC documents expire on a date.
/// Every one of those needs to be testable without sleeping, so nothing outside this
/// interface is permitted to ask the system what time it is.
/// <para>
/// Always UTC. Rendering in Asia/Manila is a presentation concern.
/// </para>
/// </remarks>
public interface IClock
{
    DateTime UtcNow { get; }
}
