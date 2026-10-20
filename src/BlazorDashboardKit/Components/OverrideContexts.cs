using System;
using BlazorDashboardKit.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorDashboardKit.Components;

/// <summary>Context for an empty-dashboard override (no widgets on the active dashboard).</summary>
public sealed record EmptyDashboardContext(bool EditMode);

/// <summary>
/// Context for a custom "add widget" picker. <see cref="Available"/> is already
/// filtered by <c>IWidgetAccessControl</c>; invoke <see cref="Add"/> with a
/// descriptor to add that widget.
/// </summary>
public sealed record WidgetPickerContext(
    System.Collections.Generic.IReadOnlyList<WidgetDescriptor> Available,
    EventCallback<WidgetDescriptor> Add);

/// <summary>
/// Context for a custom dashboard toolbar, which replaces the kit's own (picker,
/// Edit/Done, name box, + Dashboard, Delete, Add Widget, Actions). It carries the
/// state the toolbar shows and every action it can take. <see cref="Busy"/> is true
/// while a widget is in its own edit sub-mode; the kit's toolbar disables its
/// actions then. <see cref="Import"/> takes the export file's JSON text and
/// replaces the active dashboard with it. <see cref="MaxNameLength"/> is the
/// host's limit on dashboard names (null for none); the kit trims longer names.
/// </summary>
public sealed record DashboardToolbarContext(
    System.Collections.Generic.IReadOnlyList<DashboardDefinition> Dashboards,
    DashboardDefinition Active,
    bool EditMode,
    bool ReadOnly,
    bool Busy,
    int? MaxNameLength,
    System.Collections.Generic.IReadOnlyList<WidgetDescriptor> AvailableWidgets,
    EventCallback EnterEditMode,
    EventCallback ExitEditMode,
    EventCallback<string> Switch,
    EventCallback AddDashboard,
    EventCallback<string> Rename,
    EventCallback DeleteActive,
    EventCallback Export,
    EventCallback<string> Import,
    EventCallback<WidgetDescriptor> AddWidget)
{
    /// <summary>The last dashboard can't be deleted.</summary>
    public bool CanDelete => Dashboards.Count > 1;
}

/// <summary>
/// Context for a custom config-panel shell. The kit still owns the working-copy
/// clone and Save/Cancel semantics; the shell only supplies chrome around
/// <see cref="Body"/> (the widget's config component, or a "no configuration"
/// message) and wires its own controls to <see cref="Save"/>/<see cref="Cancel"/>.
/// </summary>
public sealed record WidgetConfigShellContext(
    RenderFragment Body,
    bool HasConfig,
    EventCallback Save,
    EventCallback Cancel);

/// <summary>
/// Context for a "widget type not registered" override. The placement still
/// exists in storage; <see cref="Remove"/> deletes it.
/// </summary>
public sealed record WidgetUnavailableContext(
    WidgetPlacement Placement,
    bool EditMode,
    EventCallback Remove);

/// <summary>
/// Context for a widget-error override (the widget component threw and was
/// caught by the kit's error boundary). <see cref="Recover"/> resets the
/// boundary and re-renders the widget.
/// </summary>
public sealed record WidgetErrorContext(
    Exception Exception,
    WidgetPlacement Placement,
    WidgetDescriptor Descriptor,
    EventCallback Recover);
