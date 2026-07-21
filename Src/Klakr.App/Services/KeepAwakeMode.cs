namespace Klakr.App.Services;

/// <summary>
/// The method Keep Awake uses to keep the machine looking active. Mutually exclusive - one
/// mode at a time, matching how the original Caffeine models this (with an extra
/// idle-aware variant Caffeine doesn't have).
/// </summary>
public enum KeepAwakeMode
{
    /// <summary>Send the configured key only after N seconds of no real input. Default.</summary>
    SimulateKeyIdleOnly,

    /// <summary>Send the configured key every N seconds regardless of user activity.</summary>
    SimulateKeyAlways,

    /// <summary>Windows-only: SetThreadExecutionState prevents sleep AND display off. No key.</summary>
    PreventSleep,

    /// <summary>Windows-only: SetThreadExecutionState prevents sleep only; screensaver allowed. No key.</summary>
    PreventSleepAllowScreensaver,
}

public static class KeepAwakeModeExtensions
{
    /// <summary>True when the mode sends a synthetic key (either variant).</summary>
    public static bool SendsKey(this KeepAwakeMode mode)
        => mode is KeepAwakeMode.SimulateKeyIdleOnly or KeepAwakeMode.SimulateKeyAlways;

    /// <summary>True when the mode calls SetThreadExecutionState.</summary>
    public static bool UsesStes(this KeepAwakeMode mode)
        => mode is KeepAwakeMode.PreventSleep or KeepAwakeMode.PreventSleepAllowScreensaver;

    public static string DisplayName(this KeepAwakeMode mode) => mode switch
    {
        KeepAwakeMode.SimulateKeyIdleOnly => "Simulate key (idle only)",
        KeepAwakeMode.SimulateKeyAlways => "Simulate key (always)",
        KeepAwakeMode.PreventSleep => "Prevent sleep",
        KeepAwakeMode.PreventSleepAllowScreensaver => "Prevent sleep (allow screensaver)",
        _ => mode.ToString(),
    };
}
