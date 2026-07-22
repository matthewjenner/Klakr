using Avalonia;
using System;
using Velopack;

namespace Klakr.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Must be the first call in Main, exactly once. Handles Velopack install/update/uninstall
        // hook events and exits before Avalonia starts when invoked for those.
        VelopackApp.Build().Run();

        // Second-launch guard: if Klakr is already running, hand the "show yourself" request
        // to the primary and exit without starting a second Avalonia stack (which would spawn
        // a duplicate tray icon and process). The startup auto-launch (--minimized) that
        // races an already-running instance exits silently rather than popping the window.
        if (!SingleInstance.TryAcquire())
        {
            if (!AppArgs.StartMinimized)
                SingleInstance.SignalActivate();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstance.Release();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
