using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Klakr.App.Services;
using Klakr.App.ViewModels;

namespace Klakr.App.Views;

/// <summary>The profile / sequence editor window.</summary>
public partial class ConfigWindow : Window
{
    public ConfigWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosingPersistPosition;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not ConfigWindowViewModel vm)
            return;
        AppSettings s = vm.Host.Settings;
        bool restored = WindowPlacement.Restore(this,
            s.ConfigWindowX, s.ConfigWindowY, s.ConfigWindowWidth, s.ConfigWindowHeight);
        if (restored)
            DiagLog.ConfigWindowRestored(s.ConfigWindowX!.Value, s.ConfigWindowY!.Value,
                s.ConfigWindowWidth!.Value, s.ConfigWindowHeight!.Value);
        else if (s.ConfigWindowX is not null)
            DiagLog.ConfigWindowOffScreen(s.ConfigWindowX.Value, s.ConfigWindowY ?? 0);
    }

    private void OnClosingPersistPosition(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not ConfigWindowViewModel vm)
            return;
        (int x, int y, int w, int h) = WindowPlacement.Snapshot(this);
        vm.Host.UpdateSettings(vm.Host.Settings with
        {
            ConfigWindowX = x,
            ConfigWindowY = y,
            ConfigWindowWidth = w,
            ConfigWindowHeight = h,
        });
        DiagLog.ConfigWindowSaved(x, y, w, h);
    }

    /// <summary>
    /// Copy-diagnostics is here in code-behind (not a RelayCommand) because the clipboard is a
    /// UI-thread concern owned by the top-level Window, not something a ViewModel can reach.
    /// <see cref="ClipboardExtensions.SetTextAsync"/> is the Avalonia 12 way to write text.
    /// </summary>
    private async void OnCopyDiagnosticsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConfigWindowViewModel vm)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        // Refresh live values (update timestamp, NVAPI, etc.) before snapshotting.
        vm.RefreshUpdateStatus();

        try
        {
            await clipboard.SetTextAsync(vm.CurrentDiagnosticsText);
            vm.CopyDiagnosticsStatus = "Copied.";
        }
        catch
        {
            vm.CopyDiagnosticsStatus = "Copy failed.";
        }
    }
}
