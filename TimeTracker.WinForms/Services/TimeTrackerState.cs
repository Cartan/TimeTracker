using TimeTracker.WinForms.Models;

namespace TimeTracker.WinForms.Services;

public sealed class TimeTrackerState
{
    public TimeEntryDto? ActiveEntry { get; private set; }

    public void SetEntries(IEnumerable<TimeEntryDto> entries)
    {
        ActiveEntry = entries
            .Where(entry => entry.EndTime is null)
            .OrderByDescending(entry => entry.StartTime)
            .FirstOrDefault();
    }

    public TimeSpan GetElapsed(DateTimeOffset now)
    {
        if (ActiveEntry is null)
        {
            return TimeSpan.Zero;
        }

        var elapsed = now - ActiveEntry.StartTime;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    public void Clear() => ActiveEntry = null;
}
