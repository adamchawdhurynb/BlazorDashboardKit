namespace BlazorDashboardKit.Models;

/// <summary>
/// Tunable GridStack options for the dashboard surface. Defaults match the
/// kit's built-in behaviour; pass an instance to <c>DashboardHost.GridOptions</c>
/// to reuse the kit with different column counts, density, or responsiveness.
/// </summary>
public sealed class DashboardGridOptions
{
    /// <summary>Number of columns the grid is divided into.</summary>
    public int Columns { get; set; } = 12;

    /// <summary>Row height in pixels.</summary>
    public int CellHeight { get; set; } = 146;

    /// <summary>Gap between widgets in pixels.</summary>
    public int Margin { get; set; } = 8;

    /// <summary>Free placement (true) vs. gravity/compaction (false) outside edit mode. Edit mode always compacts.</summary>
    public bool Float { get; set; } = true;

    /// <summary>Viewport width (px) at or below which the grid collapses to <see cref="MobileColumns"/>.</summary>
    public int MobileBreakpointWidth { get; set; } = 992;

    /// <summary>Column count used below <see cref="MobileBreakpointWidth"/>.</summary>
    public int MobileColumns { get; set; } = 1;
}
