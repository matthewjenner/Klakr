using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Klakr.App.Services;
using Klakr.App.ViewModels;

namespace Klakr.App.Views;

/// <summary>
/// The diagnostics sidecar. Opens with logging enabled, closes with logging disabled and
/// the buffer cleared, so the log is never accumulating in the background.
/// </summary>
public partial class DiagnosticsWindow : Window
{
    private bool _autoScroll = true;
    private ScrollViewer? _scroll;
    private INotifyCollectionChanged? _observedEntries;

    private AppHost? _host;

    public DiagnosticsWindow()
    {
        InitializeComponent();

        Opened += OnOpened;
        Closing += OnClosing;
    }

    /// <summary>Called by App.axaml.cs before Show(), so the window can persist its position.</summary>
    public void AttachHost(AppHost host) => _host = host;

    private void OnOpened(object? sender, EventArgs e)
    {
        _scroll = this.FindControl<ScrollViewer>("LogScroll");
        if (_scroll is not null)
            _scroll.ScrollChanged += OnScrollChanged;

        if (DataContext is DiagnosticsWindowViewModel vm)
        {
            _observedEntries = vm.Visible;
            _observedEntries.CollectionChanged += OnVisibleChanged;
        }

        if (_host is not null)
        {
            AppSettings s = _host.Settings;
            WindowPlacement.Restore(this,
                s.DiagnosticsWindowX, s.DiagnosticsWindowY,
                s.DiagnosticsWindowWidth, s.DiagnosticsWindowHeight);
        }

        // Scroll to bottom initially in case we hydrated with a full backlog.
        Dispatcher.UIThread.Post(ScrollToBottomIfEnabled, DispatcherPriority.Loaded);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_host is not null)
        {
            (int x, int y, int w, int h) = WindowPlacement.Snapshot(this);
            _host.UpdateSettings(_host.Settings with
            {
                DiagnosticsWindowX = x,
                DiagnosticsWindowY = y,
                DiagnosticsWindowWidth = w,
                DiagnosticsWindowHeight = h,
            });
        }

        if (_scroll is not null)
            _scroll.ScrollChanged -= OnScrollChanged;
        if (_observedEntries is not null)
        {
            _observedEntries.CollectionChanged -= OnVisibleChanged;
            _observedEntries = null;
        }
        if (DataContext is DiagnosticsWindowViewModel vm)
            vm.Detach();

        DiagLog.Disable();
    }

    private void OnVisibleChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            Dispatcher.UIThread.Post(ScrollToBottomIfEnabled, DispatcherPriority.Background);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is null)
            return;
        // "At bottom" if within ~4px of the extent bottom. Otherwise the user is browsing
        // history and we pause auto-scroll until they return.
        double distanceFromBottom = _scroll.Extent.Height - _scroll.Offset.Y - _scroll.Viewport.Height;
        _autoScroll = distanceFromBottom < 4;
    }

    private void ScrollToBottomIfEnabled()
    {
        if (_autoScroll)
            _scroll?.ScrollToEnd();
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DiagnosticsWindowViewModel vm)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        try { await clipboard.SetTextAsync(vm.BuildClipboardText()); }
        catch { /* ignore - clipboard access can transiently fail */ }
    }
}
