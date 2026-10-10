namespace Candidate.API.Services;

/// <summary>
/// Source of "now" and "today" for the application, injectable so tests can fix the date.
/// </summary>
public interface IClock
{
    /// <summary>Timestamp for audit columns.</summary>
    DateTime UtcNow { get; }

    /// <summary>The business date used to decide whether a license has expired.</summary>
    DateOnly Today { get; }
}

/// <summary>
/// Business date in US Eastern time. The agency is in Georgia, and a UTC date would
/// roll over to "tomorrow" at 7 or 8 p.m. local time and expire licenses a day early.
/// </summary>
public sealed class EasternClock(TimeProvider timeProvider) : IClock
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Eastern).DateTime);
}
