using Klakr.Core.Input;
using SharpHook;
using SharpHook.Data;

namespace Klakr.App.Input;

/// <summary>
/// Bridges SharpHook (libuiohook) to Klakr.Core's platform-neutral <see cref="IInputHook"/> and
/// <see cref="IInputSimulator"/>. The only place in the app that touches SharpHook directly.
/// </summary>
/// <remarks>
/// Hook events arrive on libuiohook's background loop thread - subscribers must return quickly.
/// Synthetic events we post are flagged by libuiohook and surface with
/// <see cref="KeyEventArgs.IsSynthetic"/> set, so callers can ignore our own injected input.
/// </remarks>
public sealed class SharpHookAdapter : IInputHook, IInputSimulator
{
    private readonly IGlobalHook _hook;
    private readonly IEventSimulator _simulator;
    private bool _started;

    public SharpHookAdapter()
    {
        // Keyboard-only hook: Klakr never inspects or synthesizes mouse input.
        _hook = new SimpleGlobalHook(GlobalHookType.Keyboard);
        _simulator = new EventSimulator();
        _hook.KeyPressed += OnHookKeyPressed;
        _hook.KeyReleased += OnHookKeyReleased;
    }

    public event EventHandler<KeyEventArgs>? KeyPressed;
    public event EventHandler<KeyEventArgs>? KeyReleased;

    public void Start()
    {
        if (_started)
            return;
        _started = true;

        // RunAsync starts the libuiohook loop on a background thread and returns a Task that
        // completes when the hook stops. Observe it so a startup failure isn't silently lost.
        _ = _hook.RunAsync().ContinueWith(
            t => Console.Error.WriteLine($"Klakr input hook faulted: {t.Exception}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Stop()
    {
        if (_hook is { IsRunning: true })
            _hook.Stop();
    }

    public void PressKey(Key key)
    {
        if (KeyCodeMap.ToKeyCode(key) is { } code)
            _simulator.SimulateKeyPress(code);
    }

    public void ReleaseKey(Key key)
    {
        if (KeyCodeMap.ToKeyCode(key) is { } code)
            _simulator.SimulateKeyRelease(code);
    }

    public void Dispose()
    {
        _hook.KeyPressed -= OnHookKeyPressed;
        _hook.KeyReleased -= OnHookKeyReleased;
        _hook.Dispose();
    }

    private void OnHookKeyPressed(object? sender, KeyboardHookEventArgs e)
        => Raise(KeyPressed, e);

    private void OnHookKeyReleased(object? sender, KeyboardHookEventArgs e)
        => Raise(KeyReleased, e);

    private void Raise(EventHandler<KeyEventArgs>? handler, KeyboardHookEventArgs e)
    {
        if (handler is null)
            return;
        if (KeyCodeMap.ToKey(e.Data.KeyCode) is not { } key)
            return;

        handler(this, new KeyEventArgs(key, ToModifiers(e.RawEvent.Mask), e.IsEventSimulated));
    }

    private static KeyModifiers ToModifiers(EventMask mask)
    {
        // EventMask.Shift is (LeftShift | RightShift); test with a bit-AND so either side counts.
        var result = KeyModifiers.None;
        if ((mask & EventMask.Shift) != EventMask.None) result |= KeyModifiers.Shift;
        if ((mask & EventMask.Ctrl) != EventMask.None) result |= KeyModifiers.Ctrl;
        if ((mask & EventMask.Alt) != EventMask.None) result |= KeyModifiers.Alt;
        if ((mask & EventMask.Meta) != EventMask.None) result |= KeyModifiers.Meta;
        return result;
    }
}
