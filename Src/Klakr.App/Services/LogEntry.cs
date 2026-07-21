namespace Klakr.App.Services;

/// <summary>A single diagnostic log line. Wall-clock local time with millisecond precision.</summary>
public sealed record LogEntry(DateTime Timestamp, LogCategory Category, string Message)
{
    /// <summary>Just the timestamp, for the sidecar's dim gray timestamp column.</summary>
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    /// <summary>Just the <c>[Category]</c> tag - colored per category in the sidecar.</summary>
    public string TagText => $"[{Category}]";

    /// <summary>
    /// Full single-string form used by "Copy" to clipboard: <c>HH:mm:ss.fff [Category] Message</c>.
    /// Property (not method) so Avalonia's <c>{Binding FormatLine}</c> reads its value rather
    /// than grabbing the method group as a delegate.
    /// </summary>
    public string FormatLine => $"{TimestampText} {TagText} {Message}";
}
