using Avalonia.Media;

namespace Klakr.App.ViewModels;

/// <summary>A row in the profile list - name, enabled state, and a status colour.</summary>
public sealed record ProfileSummary(string Name, bool Enabled)
{
    /// <summary>Green when the profile is enabled (armed for its hotkey), grey when disabled.</summary>
    public IBrush StatusBrush => Enabled ? Brushes.LimeGreen : Brushes.Gray;
}
