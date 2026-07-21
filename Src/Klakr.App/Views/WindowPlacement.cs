using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Klakr.App.Views;

/// <summary>
/// Shared helpers for saving and restoring window positions across launches. Guards against
/// stale positions that fall entirely off-screen (monitor unplugged, resolution change).
/// </summary>
internal static class WindowPlacement
{
    /// <summary>
    /// Apply <paramref name="x"/>/<paramref name="y"/>/<paramref name="width"/>/<paramref name="height"/>
    /// to <paramref name="window"/> if any of the saved rect's top-left area still lands on a
    /// currently-connected screen's working area. Returns true when applied.
    /// </summary>
    public static bool Restore(Window window, int? x, int? y, int? width, int? height)
    {
        if (x is null || y is null || width is null || height is null)
            return false;
        if (width.Value <= 0 || height.Value <= 0)
            return false;

        Screen[]? screens = window.Screens?.All is { } list ? [.. list] : null;
        if (screens is null || screens.Length == 0)
            return false;

        var topLeft = new PixelPoint(x.Value, y.Value);
        bool onScreen = false;
        foreach (Screen screen in screens)
        {
            if (screen.WorkingArea.Contains(topLeft))
            {
                onScreen = true;
                break;
            }
        }
        if (!onScreen)
            return false;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = topLeft;
        window.Width = width.Value;
        window.Height = height.Value;
        return true;
    }

    /// <summary>Reads the current position + size from <paramref name="window"/>.</summary>
    public static (int X, int Y, int Width, int Height) Snapshot(Window window)
    {
        PixelPoint pos = window.Position;
        int w = (int)Math.Round(window.Width);
        int h = (int)Math.Round(window.Height);
        return (pos.X, pos.Y, w, h);
    }
}
