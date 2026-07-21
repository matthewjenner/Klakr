using CommunityToolkit.Mvvm.ComponentModel;

namespace Klakr.App.ViewModels;

/// <summary>
/// One hour cell in the Keep-Awake tab's active-hours grid. The parent VM wires a callback
/// so any toggle rebuilds the 24-bit mask on the settings record.
/// </summary>
public sealed partial class HourToggleViewModel : ObservableObject
{
    private readonly Action<HourToggleViewModel>? _onChanged;

    public HourToggleViewModel(int hour, bool isActive, Action<HourToggleViewModel>? onChanged)
    {
        Hour = hour;
        _isActive = isActive;
        _onChanged = onChanged;
    }

    /// <summary>0-23. Used as the bit index in the mask.</summary>
    public int Hour { get; }

    /// <summary>Two-digit label for the ToggleButton content ("00" through "23").</summary>
    public string Label => Hour.ToString("00");

    [ObservableProperty]
    private bool _isActive;

    partial void OnIsActiveChanged(bool value) => _onChanged?.Invoke(this);
}
