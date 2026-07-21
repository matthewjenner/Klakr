namespace Klakr.App.Services;

/// <summary>A single diagnostic log line. Wall-clock local time with millisecond precision.</summary>
public sealed record LogEntry(DateTime Timestamp, LogCategory Category, string Message)
{
    /// <summary>Formatted for the sidecar's list view: <c>HH:mm:ss.fff [Category] Message</c>.</summary>
    public string FormatLine()
        => $"{Timestamp:HH:mm:ss.fff} [{Category}] {Message}";
}
