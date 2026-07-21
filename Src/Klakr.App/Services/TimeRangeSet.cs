using System.Globalization;

namespace Klakr.App.Services;

/// <summary>
/// A set of daily time-of-day ranges, e.g. "09:00-17:00, 20:00-23:00". Used by Keep Awake
/// to restrict activity to specific hours. An empty set means "always allowed".
/// </summary>
public sealed class TimeRangeSet
{
    private readonly List<(TimeOnly Start, TimeOnly End)> _ranges;

    public static readonly TimeRangeSet Always = new([]);

    private TimeRangeSet(List<(TimeOnly, TimeOnly)> ranges)
    {
        _ranges = ranges;
    }

    /// <summary>True when there are no ranges (Keep Awake is not time-limited).</summary>
    public bool IsUnlimited => _ranges.Count == 0;

    /// <summary>Number of parsed ranges (for the diagnostic status line).</summary>
    public int Count => _ranges.Count;

    /// <summary>True if <paramref name="now"/> falls within any of the configured ranges.</summary>
    public bool Contains(TimeOnly now)
    {
        if (IsUnlimited)
            return true;

        foreach ((TimeOnly start, TimeOnly end) in _ranges)
        {
            if (start <= end)
            {
                if (now >= start && now <= end)
                    return true;
            }
            else
            {
                // Range wraps midnight (e.g. 22:00-06:00). Match either side.
                if (now >= start || now <= end)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Parses a comma-separated list of <c>HH:MM-HH:MM</c> ranges. Whitespace around commas
    /// and hyphens is ignored. Malformed entries are silently dropped so a typo in one range
    /// doesn't disable the rest. Returns <see cref="Always"/> for empty / null input.
    /// </summary>
    public static TimeRangeSet Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Always;

        var ranges = new List<(TimeOnly, TimeOnly)>();
        foreach (string chunk in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int dash = chunk.IndexOf('-');
            if (dash <= 0 || dash >= chunk.Length - 1)
                continue;

            string startStr = chunk[..dash].Trim();
            string endStr = chunk[(dash + 1)..].Trim();

            if (TimeOnly.TryParseExact(startStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly start)
                && TimeOnly.TryParseExact(endStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly end))
            {
                ranges.Add((start, end));
            }
        }
        return new TimeRangeSet(ranges);
    }
}
