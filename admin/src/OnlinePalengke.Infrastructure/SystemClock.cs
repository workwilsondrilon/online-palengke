using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Infrastructure;

/// <summary>The real clock. Tests substitute a fake so deadlines can be driven directly.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
