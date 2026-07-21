using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klakr.App.Services;

namespace Klakr.App.ViewModels;

/// <summary>
/// Backs the diagnostics sidecar window. Hydrates from <see cref="DiagLog.Snapshot"/>,
/// then streams new entries via <see cref="DiagLog.EntryAdded"/>. Filters are soft - every
/// entry is retained; unchecked categories just aren't shown.
/// </summary>
public sealed partial class DiagnosticsWindowViewModel : ObservableObject
{
    private readonly List<LogEntry> _all = [];

    public DiagnosticsWindowViewModel()
    {
        // Hydrate from any entries that landed between Enable() and this window's open.
        foreach (LogEntry entry in DiagLog.Snapshot())
            _all.Add(entry);
        RebuildVisible();

        DiagLog.EntryAdded += OnEntryAdded;
    }

    /// <summary>The list bound to the sidecar's ItemsControl. Filtered subset of <see cref="_all"/>.</summary>
    public ObservableCollection<LogEntry> Visible { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowSystem))]    private bool _showSystemFilter = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEngine))]    private bool _showEngineFilter = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowKeepAwake))] private bool _showKeepAwakeFilter = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowDisplay))]   private bool _showDisplayFilter = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowKeyInput))]  private bool _showKeyInputFilter = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowUpdate))]    private bool _showUpdateFilter = true;

    public bool ShowSystem    => ShowSystemFilter;
    public bool ShowEngine    => ShowEngineFilter;
    public bool ShowKeepAwake => ShowKeepAwakeFilter;
    public bool ShowDisplay   => ShowDisplayFilter;
    public bool ShowKeyInput  => ShowKeyInputFilter;
    public bool ShowUpdate    => ShowUpdateFilter;

    partial void OnShowSystemFilterChanged(bool value)    => RebuildVisible();
    partial void OnShowEngineFilterChanged(bool value)    => RebuildVisible();
    partial void OnShowKeepAwakeFilterChanged(bool value) => RebuildVisible();
    partial void OnShowDisplayFilterChanged(bool value)   => RebuildVisible();
    partial void OnShowKeyInputFilterChanged(bool value)  => RebuildVisible();
    partial void OnShowUpdateFilterChanged(bool value)    => RebuildVisible();

    /// <summary>Wipes the buffer AND the visible list; useful when starting a fresh test.</summary>
    [RelayCommand]
    private void Clear()
    {
        _all.Clear();
        Visible.Clear();
        // Also flush DiagLog's internal buffer so a subsequent Snapshot returns empty.
        DiagLog.Disable();
        DiagLog.Enable();
    }

    private void OnEntryAdded(LogEntry entry)
    {
        _all.Add(entry);
        // Match DiagLog's buffer bound so we don't grow indefinitely in-window.
        while (_all.Count > DiagLog.BufferLimit)
            _all.RemoveAt(0);

        if (IsCategoryVisible(entry.Category))
            Visible.Add(entry);

        // Trim the visible list too, so scrolling stays bounded.
        while (Visible.Count > DiagLog.BufferLimit)
            Visible.RemoveAt(0);
    }

    private void RebuildVisible()
    {
        Visible.Clear();
        foreach (LogEntry entry in _all)
        {
            if (IsCategoryVisible(entry.Category))
                Visible.Add(entry);
        }
    }

    private bool IsCategoryVisible(LogCategory category) => category switch
    {
        LogCategory.System    => ShowSystemFilter,
        LogCategory.Engine    => ShowEngineFilter,
        LogCategory.KeepAwake => ShowKeepAwakeFilter,
        LogCategory.Display   => ShowDisplayFilter,
        LogCategory.KeyInput  => ShowKeyInputFilter,
        LogCategory.Update    => ShowUpdateFilter,
        _ => true,
    };

    /// <summary>Joins the CURRENT VISIBLE (post-filter) list as plain text for clipboard.</summary>
    public string BuildClipboardText()
    {
        return string.Join(Environment.NewLine, Visible.Select(e => e.FormatLine()));
    }

    /// <summary>Called by the window on Closing so the DiagLog subscription doesn't leak.</summary>
    public void Detach()
    {
        DiagLog.EntryAdded -= OnEntryAdded;
    }
}
