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
    private DiagnosticsWindow? _diagnosticsWindow;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _keepAwakeItem;
    private CancellationTokenSource? _activationListenerCts;
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The config window closes to the tray; the app quits only via the tray menu.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Peek the sidecar flag BEFORE the AppHost ctor fires its startup log points
            // (settings migration, profile reload). Enabling here means those calls land in
            // the buffer and the auto-opened sidecar shows them - the whole point of persisting
            // sidecar-open state is being able to see startup activity.
            bool autoOpenSidecar = SettingsStore.TryPeekSidecarOpen(ProfilePaths.SettingsFilePath);
            if (autoOpenSidecar)
                DiagLog.Enable();

            _host = new AppHost();

            // Refresh the HKCU Run entry so it always points at the currently-running exe.
            // Velopack updates move the exe between subfolders; this catches that.
            if (OperatingSystem.IsWindows() && _host.Settings.StartWithWindows)
                AutoStart.Enable();

            var configVm = new ConfigWindowViewModel(_host);
            configVm.DiagnosticsLogRequested += OnDiagnosticsLogRequested;
            _configWindow = new ConfigWindow
            {
                DataContext = configVm,
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

            // Enabled starts false; the startup log line will only appear if the user later
            // toggles the sidecar on. Emit it anyway so it's the first entry they see when
            // they DO open the sidecar (well - would be, if we were logging pre-enable).
            // In practice: this is a no-op unless someone rewires DiagLog.
            DiagLog.AppStarted(
                AppVersion.Display,
                System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim(),
                _host.IsNvidiaAvailable,
                AppArgs.StartMinimized);

            // Auto-open the sidecar now that the app is fully wired. Setting the VM property
            // cascades through OnShowDiagnosticsLogChanged -> DiagnosticsLogRequested ->
            // OpenDiagnosticsWindow(). DiagLog is already enabled from the early peek above,
            // and Enable() is idempotent, so we don't wipe the startup log buffer.
            if (autoOpenSidecar)
                configVm.ShowDiagnosticsLog = true;

            // Listen for "show yourself" signals from secondary launches (Program.Main's
            // SingleInstance guard forwards them here). Runs until Quit() cancels the token.
            _activationListenerCts = new CancellationTokenSource();
            _ = SingleInstance.ListenAsync(
                () => Dispatcher.UIThread.Post(OnActivationRequested),
                _activationListenerCts.Token);
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
        // Sidecar (if open) intentionally stays alive - it's independent of the config
        // window's visibility, and closing config to the tray shouldn't kill an in-progress
        // diagnostic session.
    }

    private void OnDiagnosticsLogRequested(bool show)
    {
        if (show)
            OpenDiagnosticsWindow();
        else
            CloseDiagnosticsWindow();
    }

    private void OpenDiagnosticsWindow()
    {
        if (_host is null)
            return;
        if (_diagnosticsWindow is not null)
        {
            _diagnosticsWindow.Activate();
            return;
        }

        DiagLog.Enable();
        // Persist that the user (or startup-restore) opened the sidecar. Cleared only by
        // explicit close - a kill or quit-via-tray preserves this so the next launch
        // reopens automatically and captures startup logs.
        _host.UpdateSettings(_host.Settings with { DiagnosticsSidecarOpen = true });

        _diagnosticsWindow = new DiagnosticsWindow
        {
            DataContext = new DiagnosticsWindowViewModel(),
        };
        _diagnosticsWindow.AttachHost(_host);
        _diagnosticsWindow.Closed += OnDiagnosticsWindowClosed;
        // Independent window (no owner). Config close-to-tray leaves the sidecar visible;
        // owner-tied windows would minimise with the owner on Windows.
        _diagnosticsWindow.Show();
    }

    private void CloseDiagnosticsWindow()
    {
        if (_diagnosticsWindow is null)
            return;
        // Closed handler will null the field and reflect the state back to the checkbox.
        _diagnosticsWindow.Close();
    }

    private void OnDiagnosticsWindowClosed(object? sender, EventArgs e)
    {
        if (_diagnosticsWindow is not null)
        {
            _diagnosticsWindow.Closed -= OnDiagnosticsWindowClosed;
            _diagnosticsWindow = null;
        }

        // Only clear the persisted "sidecar is open" flag on EXPLICIT user close. If we're
        // in the middle of a Quit(), preserve the flag so next launch reopens the sidecar.
        if (!_quitting && _host is not null)
            _host.UpdateSettings(_host.Settings with { DiagnosticsSidecarOpen = false });

        // DiagLog.Disable() is called by DiagnosticsWindow.OnClosing.
        // Reflect state back to the checkbox so it stays in sync if the window closed via X.
        if (_configWindow?.DataContext is ConfigWindowViewModel vm && vm.ShowDiagnosticsLog)
            vm.ShowDiagnosticsLog = false;
    }

    private void ShowConfigWindow()
    {
        if (_configWindow is null)
            return;
        _configWindow.Show();
        _configWindow.Activate();
    }

    private void OnActivationRequested()
    {
        DiagLog.SecondLaunchActivated();
        ShowConfigWindow();
    }

    private void Quit()
    {
        _quitting = true;
        _activationListenerCts?.Cancel();
        _host?.Dispose();
        _trayIcon?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
