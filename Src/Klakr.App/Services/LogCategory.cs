namespace Klakr.App.Services;

/// <summary>
/// The category of a diagnostic log entry. The sidecar window offers a per-category
/// filter, so keep this list short and the categories meaningful.
/// </summary>
public enum LogCategory
{
    /// <summary>Startup, settings persistence, profile store, auto-start toggles.</summary>
    System,
    /// <summary>Sequence engine start/stop and cancellation.</summary>
    Engine,
    /// <summary>Keep Awake state changes, key sends, STES apply/clear, timed-on expiry.</summary>
    KeepAwake,
    /// <summary>NVIDIA Display preset applies and default restores.</summary>
    Display,
    /// <summary>Every synthesized key press/release (high-volume category).</summary>
    KeyInput,
    /// <summary>GitHub update checks and their results.</summary>
    Update,
}
