using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Klakr.App.Platform.Windows;

namespace Klakr.App.Views;

/// <summary>
/// The always-on-top status dot. Borderless, transparent, non-focusable and - on Windows -
/// click-through, so it never intercepts input meant for the game underneath.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int MarginDip = 12;

    // How often to re-assert topmost so borderless-fullscreen games can't keep the dot buried.
    private static readonly TimeSpan TopmostInterval = TimeSpan.FromSeconds(1);

    private DispatcherTimer? _topmostTimer;
    private bool _opened;

    private OverlayAnchor _anchor = OverlayAnchor.TopRight;
    private int _offsetX;
    private int _offsetY;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Places the dot at <paramref name="anchor"/>, nudged by the given DIP offsets. Safe to
    /// call before or after the window opens; repositions live once it is open.
    /// </summary>
    public void ApplyPosition(OverlayAnchor anchor, int offsetXDip, int offsetYDip)
    {
        _anchor = anchor;
        _offsetX = offsetXDip;
        _offsetY = offsetYDip;
        if (_opened)
            PositionOnScreen();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _opened = true;

        // The native handle exists once the window is open.
        if (OperatingSystem.IsWindows()
            && TryGetPlatformHandle()?.Handle is { } handle
            && handle != 0)
        {
            ClickThrough.Enable(handle);
            StartTopmostGuard(handle);
        }

        PositionOnScreen();
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer?.Stop();
        _topmostTimer = null;
        base.OnClosed(e);
    }

    private void StartTopmostGuard(nint handle)
    {
        // Topmost is not absolute on Windows - a borderless-fullscreen game re-asserts its own
        // topmost z-order and buries the overlay. Re-assert ours periodically so it stays visible.
        _topmostTimer = new DispatcherTimer { Interval = TopmostInterval };
        _topmostTimer.Tick += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
                TopmostGuard.Reassert(handle);
        };
        _topmostTimer.Start();
    }

    private void PositionOnScreen()
    {
        Screen? screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
            return;

        PixelRect area = screen.WorkingArea;
        double scaling = screen.Scaling;
        int width = (int)(Width * scaling);
        int height = (int)(Height * scaling);
        int margin = (int)(MarginDip * scaling);

        bool left = _anchor is OverlayAnchor.TopLeft or OverlayAnchor.CenterLeft or OverlayAnchor.BottomLeft;
        bool hCenter = _anchor is OverlayAnchor.TopCenter or OverlayAnchor.Center or OverlayAnchor.BottomCenter;
        bool top = _anchor is OverlayAnchor.TopLeft or OverlayAnchor.TopCenter or OverlayAnchor.TopRight;
        bool vCenter = _anchor is OverlayAnchor.CenterLeft or OverlayAnchor.Center or OverlayAnchor.CenterRight;

        int x = left ? area.X + margin
              : hCenter ? area.X + ((area.Width - width) / 2)
              : area.Right - width - margin;
        int y = top ? area.Y + margin
              : vCenter ? area.Y + ((area.Height - height) / 2)
              : area.Bottom - height - margin;

        x += (int)(_offsetX * scaling);
        y += (int)(_offsetY * scaling);

        Position = new PixelPoint(x, y);
    }
}
