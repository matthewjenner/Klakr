using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Klakr.App.Platform.Windows;
using Klakr.App.Services;
using Klakr.App.ViewModels;
using Klakr.App.Views;

namespace Klakr.App;

public partial class App : Application
{
    private AppHost? _host;
    private ConfigWindow? _configWindow;
    private OverlayWindow? _overlay;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _keepAwakeItem;
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The config window closes to the tray; the app quits only via the tray menu.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _host = new AppHost();

            // Refresh the HKCU Run entry so it always points at the currently-running exe.
            // Velopack updates move the exe between subfolders; this catches that.
            if (OperatingSystem.IsWindows() && _host.Settings.StartWithWindows)
                AutoStart.Enable();

            _configWindow = new ConfigWindow
            {
                DataContext = new ConfigWindowViewModel(_host),
            };
            _configWindow.Closing += OnConfigWindowClosing;

            // On boot Windows launches Klakr with --minimized. We create the window and wire
            // it up as MainWindow (for ShowConfigWindow to work) but skip the initial Show(),
            // so the app lands quietly in the tray. Manual launch (no flag) shows the window.
            if (!AppArgs.StartMinimized)
                desktop.MainWindow = _configWindow;

            // The overlay is an independent top-level window so it stays visible when the
            // config window is hidden to the tray.
            _overlay = new OverlayWindow
            {
                DataContext = new OverlayWindowViewModel(_host.Engine),
            };
            ApplyOverlaySettings(_host.Settings);
            _host.SettingsChanged += ApplyOverlaySettings;

            _trayIcon = CreateTrayIcon();
            TrayIcon.SetIcons(this, [_trayIcon]);

            // The overlay is shown (or not) by ApplyOverlaySettings, per the saved setting.
            _host.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon CreateTrayIcon()
    {
        var icon = new TrayIcon
        {
            IsVisible = true,
        };

        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://Klakr.App/Assets/icon.ico"));
            icon.Icon = new WindowIcon(stream);
        }
        catch
        {
            // A missing tray image is non-fatal - the menu still works.
        }

        // Disabled item at the top of the menu - just a place to see what version is running.
        var version = new NativeMenuItem { Header = $"Klakr v{AppVersion.Display}", IsEnabled = false };

        _keepAwakeItem = new NativeMenuItem { Header = "Keep Awake: Off" };
        _keepAwakeItem.Click += (_, _) =>
        {
            if (_host is null) return;
            _host.KeepAwake.SetActive(!_host.Settings.KeepAwakeActive);
        };
        RefreshKeepAwakeItem();
        if (_host is not null)
            _host.KeepAwake.StateChanged += _ => Dispatcher.UIThread.Post(RefreshKeepAwakeItem);

        var checkForUpdates = new NativeMenuItem { Header = "Check for updates" };
        checkForUpdates.Click += async (_, _) =>
        {
            if (_host is null) return;
            await _host.Updates.CheckNowAsync();
        };

        var show = new NativeMenuItem { Header = "Show Klakr" };
        show.Click += (_, _) => ShowConfigWindow();

        var quit = new NativeMenuItem { Header = "Quit Klakr" };
        quit.Click += (_, _) => Quit();

        var menu = new NativeMenu();
        menu.Items.Add(version);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_keepAwakeItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(checkForUpdates);
        menu.Items.Add(show);
        menu.Items.Add(quit);
        icon.Menu = menu;

        icon.ToolTipText = $"Klakr v{AppVersion.Display}";

        icon.Clicked += (_, _) => ShowConfigWindow();
        return icon;
    }

    private void RefreshKeepAwakeItem()
    {
        if (_keepAwakeItem is null || _host is null)
            return;
        string state = _host.KeepAwake.CurrentState switch
        {
            KeepAwakeState.Active => "On",
            KeepAwakeState.Armed => "Armed",
            _ => "Off",
        };
        _keepAwakeItem.Header = $"Keep Awake: {state}";
    }

    private void ApplyOverlaySettings(AppSettings settings)
    {
        if (_overlay is null)
            return;

        _overlay.ApplyPosition(settings.OverlayAnchor, settings.OverlayOffsetX, settings.OverlayOffsetY);

        if (settings.OverlayVisible)
            _overlay.Show();
        else
            _overlay.Hide();
    }

    private void OnConfigWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_quitting)
            return;

        // Closing the config window hides it to the tray rather than quitting the app.
        e.Cancel = true;
        _configWindow?.Hide();
    }

    private void ShowConfigWindow()
    {
        if (_configWindow is null)
            return;
        _configWindow.Show();
        _configWindow.Activate();
    }

    private void Quit()
    {
        _quitting = true;
        _host?.Dispose();
        _trayIcon?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
