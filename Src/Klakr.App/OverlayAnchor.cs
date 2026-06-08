namespace Klakr.App;

/// <summary>
/// Where the status overlay dot is anchored on screen - a nine-point grid. A per-axis pixel
/// nudge (see <c>AppSettings</c>) shifts it from the anchor, e.g. just left/right of centre.
/// </summary>
public enum OverlayAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    Center,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}
