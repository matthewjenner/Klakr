using Avalonia.Threading;
using Klakr.App.Platform.Windows;

namespace Klakr.App.Services;

/// <summary>
/// Prevents the machine from sleeping and/or fools activity-tracking apps by simulating a
/// key press. Runs a 5-second poll loop; the poll cadence is decoupled from the user-facing
/// interval so the send-if-idle-for-N-seconds guarantee holds up to a small margin.
/// </summary>
/// <remarks>
/// State model: <see cref="AppSettings.KeepAwakeActive"/> is the master toggle. The service
/// reads it plus the time-range and timed-on-until fields every tick to decide whether it
/// should be firing right now. The Caffeine-derived modes are mutually exclusive (see
/// <see cref="KeepAwakeMode"/>).
/// </remarks>
public sealed class KeepAwakeService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly AppHost _host;
    private readonly CancellationTokenSource _cts = new();
    private DateTime _lastKeyFireUtc = DateTime.MinValue;
    private KeepAwakeState _lastState = KeepAwakeState.Off;

    public KeepAwakeService(AppHost host)
    {
        _host = host;
        ApplyStesForCurrentState();
        _ = Task.Run(() => PollAsync(_cts.Token));
    }

    /// <summary>Raised on the UI thread when the tri-state (Off / Armed / Active) changes.</summary>
    public event Action<KeepAwakeState>? StateChanged;

    /// <summary>Snapshot of the current state - Off if master off, Active if firing right now, Armed otherwise.</summary>
    public KeepAwakeState CurrentState => Evaluate();

    /// <summary>The auto-off deadline for a timed-on activation, or null if none is set.</summary>
    public DateTime? UntilUtc => _host.Settings.KeepAwakeUntilUtc;

    /// <summary>
    /// Flip the master toggle. Manually toggling always clears any timed-on deadline so a
    /// user-flip beats a countdown.
    /// </summary>
    public void SetActive(bool active)
    {
        _host.UpdateSettings(_host.Settings with
        {
            KeepAwakeActive = active,
            KeepAwakeUntilUtc = null,
        });
        DiagLog.KeepAwakeMasterToggled(active);
        Reapply();
    }

    /// <summary>
    /// Turn on for a fixed number of minutes. When the deadline passes the poll loop flips
    /// <see cref="AppSettings.KeepAwakeActive"/> back to false and clears the deadline.
    /// </summary>
    public void ActivateFor(TimeSpan duration)
    {
        _host.UpdateSettings(_host.Settings with
        {
            KeepAwakeActive = true,
            KeepAwakeUntilUtc = DateTime.UtcNow + duration,
        });
        DiagLog.KeepAwakeTimedOnStarted((int)Math.Round(duration.TotalMinutes));
        Reapply();
    }

    /// <summary>Re-evaluate now (call after the user changes mode / interval / ranges / key).</summary>
    public void Reapply()
    {
        ApplyStesForCurrentState();
        RaiseStateChangedIfChanged();
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(ct))
                Tick();
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
    }

    private void Tick()
    {
        AppSettings s = _host.Settings;

        // Timed-on expiry: master flips off, deadline cleared.
        if (s.KeepAwakeUntilUtc is DateTime until && DateTime.UtcNow >= until)
        {
            DiagLog.KeepAwakeTimedOnExpired();
            _host.UpdateSettings(s with
            {
                KeepAwakeActive = false,
                KeepAwakeUntilUtc = null,
            });
            ApplyStesForCurrentState();
            RaiseStateChangedIfChanged();
            return;
        }

        KeepAwakeState state = Evaluate();

        // Apply STES only when transitioning (avoid churn) - but for the key-simulation
        // modes STES was already cleared on activation and stays cleared.
        if (state != _lastState)
            ApplyStesForCurrentState();

        if (state == KeepAwakeState.Active)
            FireForCurrentMode(s);

        RaiseStateChangedIfChanged();
    }

    private void FireForCurrentMode(AppSettings s)
    {
        switch (s.KeepAwakeMode)
        {
            case KeepAwakeMode.SimulateKeyIdleOnly:
                if (OperatingSystem.IsWindows())
                {
                    TimeSpan idle = IdleTime.Since();
                    if (idle.TotalSeconds >= s.KeepAwakeIntervalSeconds)
                    {
                        DiagLog.KeepAwakeKeySentIdle(s.KeepAwakeKey, idle);
                        SendKey(s.KeepAwakeKey);
                    }
                }
                break;

            case KeepAwakeMode.SimulateKeyAlways:
                TimeSpan sinceLast = DateTime.UtcNow - _lastKeyFireUtc;
                if (sinceLast.TotalSeconds >= s.KeepAwakeIntervalSeconds)
                {
                    DiagLog.KeepAwakeKeySentBlind(s.KeepAwakeKey);
                    SendKey(s.KeepAwakeKey);
                }
                break;

            case KeepAwakeMode.PreventSleep:
            case KeepAwakeMode.PreventSleepAllowScreensaver:
                // STES is sticky per-thread; re-assert every tick to survive any thread
                // reassignment inside the ThreadPool the timer runs on.
                ApplyStesForCurrentState();
                break;
        }
    }

    private void SendKey(Klakr.Core.Input.Key key)
    {
        _host.PressKey(key);
        // Short hold so receivers see a proper down+up. Fire-and-forget release; the poll
        // thread returns immediately and Task.Delay + release runs on a pool thread.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(30, _cts.Token);
                _host.ReleaseKey(key);
            }
            catch (OperationCanceledException)
            {
                // Best-effort release on shutdown - AppHost.Dispose will also cover any
                // still-down state via _heldByTestTool (not applicable here) plus SharpHook
                // teardown.
            }
        });
        _lastKeyFireUtc = DateTime.UtcNow;
    }

    private KeepAwakeState Evaluate()
    {
        AppSettings s = _host.Settings;
        if (!s.KeepAwakeActive)
            return KeepAwakeState.Off;

        TimeRangeSet ranges = TimeRangeSet.Parse(s.KeepAwakeTimeRanges);
        TimeOnly nowLocal = TimeOnly.FromDateTime(DateTime.Now);
        return ranges.Contains(nowLocal) ? KeepAwakeState.Active : KeepAwakeState.Armed;
    }

    private void ApplyStesForCurrentState()
    {
        if (!OperatingSystem.IsWindows())
            return;

        AppSettings s = _host.Settings;
        KeepAwakeState state = Evaluate();

        if (state != KeepAwakeState.Active)
        {
            StayAwake.Clear();
            return;
        }

        switch (s.KeepAwakeMode)
        {
            case KeepAwakeMode.PreventSleep:
                StayAwake.KeepDisplayOn();
                DiagLog.KeepAwakeStesApplied("prevent sleep + display off");
                break;
            case KeepAwakeMode.PreventSleepAllowScreensaver:
                StayAwake.KeepSystemOn();
                DiagLog.KeepAwakeStesApplied("prevent sleep only");
                break;
            default:
                // Key-simulation modes don't use STES.
                StayAwake.Clear();
                DiagLog.KeepAwakeStesCleared();
                break;
        }
    }

    private void RaiseStateChangedIfChanged()
    {
        KeepAwakeState state = Evaluate();
        if (state == _lastState)
            return;
        DiagLog.KeepAwakeStateChanged(_lastState, state);
        _lastState = state;
        Dispatcher.UIThread.Post(() => StateChanged?.Invoke(state));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (OperatingSystem.IsWindows())
            StayAwake.Clear();
    }
}

/// <summary>Tri-state visible in the tab bubble and tray menu.</summary>
public enum KeepAwakeState
{
    /// <summary>Master toggle off.</summary>
    Off,
    /// <summary>Master on but not firing (e.g. outside allowed time range).</summary>
    Armed,
    /// <summary>Master on and actively fooling apps / preventing sleep.</summary>
    Active,
}
