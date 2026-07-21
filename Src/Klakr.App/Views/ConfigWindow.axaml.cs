using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Klakr.App.ViewModels;

namespace Klakr.App.Views;

/// <summary>The profile / sequence editor window.</summary>
public partial class ConfigWindow : Window
{
    public ConfigWindow()
    {
        InitializeComponent();
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
