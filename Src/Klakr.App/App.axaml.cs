using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The config window closes to the tray; the app quits only via the tray menu.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _host = new AppHost();

            _configWindow = new ConfigWindow
            {
                DataContext = new ConfigWindowViewModel(_host),
            };
            _configWindow.Closing += OnConfigWindowClosing;
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
            ToolTipText = "Klakr",
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

        var show = new NativeMenuItem { Header = "Show Klakr" };
        show.Click += (_, _) => ShowConfigWindow();

        var quit = new NativeMenuItem { Header = "Quit Klakr" };
        quit.Click += (_, _) => Quit();

        var menu = new NativeMenu();
        menu.Items.Add(show);
        menu.Items.Add(quit);
        icon.Menu = menu;

        icon.Clicked += (_, _) => ShowConfigWindow();
        return icon;
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
