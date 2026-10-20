using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorDashboardKit.Services;

/// <summary>
/// Render-mode-aware wrapper around the Gridstack / chart JS interop.
/// </summary>
/// <remarks>
/// <para>
/// The original BTCPay <c>DashboardJsInterop</c> gated every JS call behind a
/// server-only <c>IsPreRendering()</c> reflection check. That does not work for a
/// library that also targets WebAssembly and static SSR, so instead the host
/// supplies an explicit <paramref name="interactive"/> flag (from
/// <c>RendererInfo.IsInteractive</c>) and JS is only invoked when interactive.
/// </para>
/// <para>
/// The global <c>window.DashboardInterop</c> object is replaced by an ESM module
/// imported from the RCL static assets path
/// <c>./_content/BlazorDashboardKit/dashboard-interop.js</c>. Only
/// <see cref="InitGridAsync"/> imports the module; every other JS-invoking method
/// is a no-op until an interactive grid has been initialized, so nothing touches
/// JS during prerender / static SSR.
/// </para>
/// </remarks>
public sealed class DashboardJsInterop : IAsyncDisposable
{
    private const string ModulePath = "./_content/BlazorDashboardKit/dashboard-interop.js";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<object>? _dotNetRef;

    public DashboardJsInterop(IJSRuntime js) => _js = js;

    // --- Gridstack integration ---

    /// <summary>
    /// Imports the interop module and initializes the Gridstack grid.
    /// No-op when <paramref name="interactive"/> is <c>false</c> (static SSR / prerender).
    /// </summary>
    /// <param name="dotNetHelper">
    /// Optional .NET callback reference (e.g. for <c>OnGridChanged</c>). It is borrowed:
    /// the caller (<c>DashboardHost</c>) owns and disposes it. When supplied it is cached
    /// for forwarding into the grid; passing a new one replaces the cached reference
    /// without disposing the previous (borrowed) one.
    /// </param>
    public async Task InitGridAsync(
        string containerId,
        bool interactive,
        CancellationToken ct,
        DotNetObjectReference<object>? dotNetHelper = null,
        bool editMode = false,
        Models.DashboardGridOptions? options = null)
    {
        if (!interactive)
            return;                                     // static SSR / prerender: no JS

        if (dotNetHelper is not null && !ReferenceEquals(dotNetHelper, _dotNetRef))
        {
            // Borrowed reference: cache it for forwarding only, never dispose it
            // here — DashboardHost is its sole owner.
            _dotNetRef = dotNetHelper;
        }

        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", ct, ModulePath);
        await _module.InvokeVoidAsync("initGrid", ct, containerId, _dotNetRef, editMode,
            options ?? new Models.DashboardGridOptions());
    }

    public async Task SetEditModeAsync(bool editMode, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("setEditMode", ct, editMode);
    }

    public async Task DestroyGridAsync(bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("destroyGrid", ct);
    }

    public async Task SetWidgetLockedAsync(string widgetId, bool locked, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("setWidgetLocked", ct, widgetId, locked);
    }

    public async Task AddGridWidgetAsync(ElementReference element, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("addGridWidget", ct, element);
    }

    public async Task RemoveGridWidgetAsync(ElementReference element, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("removeGridWidget", ct, element);
    }

    // --- Charts ---

    public async Task RenderChartAsync(
        string elementId,
        string type,
        object labels,
        object series,
        bool interactive,
        CancellationToken ct,
        object? options = null)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("renderChart", ct, elementId, type, labels, series, options);
    }

    public async Task DestroyChartAsync(string elementId, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("destroyChart", ct, elementId);
    }

    // --- Export / Import (position serialization) ---

    public async Task DownloadJsonAsync(string filename, string json, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("downloadJson", ct, filename, json);
    }

    public async Task<string?> ReadFileAsTextAsync(ElementReference input, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return null;
        return await _module.InvokeAsync<string?>("readFileAsText", ct, input);
    }

    public async Task CopyToClipboardAsync(string text, bool interactive, CancellationToken ct)
    {
        if (!interactive || _module is null)
            return;
        await _module.InvokeVoidAsync("copyToClipboard", ct, text);
    }

    // --- .NET callback reference ---

    /// <summary>
    /// Caches the .NET callback reference for forwarding into the grid.
    /// Does not invoke JS; <see cref="InitGridAsync"/> forwards the cached reference
    /// to the grid. The reference is borrowed — its owner (<c>DashboardHost</c>) is
    /// solely responsible for disposal; replacing it here does not dispose the prior one.
    /// </summary>
    public void RegisterDotNetRef(DotNetObjectReference<object> dotNetHelper)
    {
        if (ReferenceEquals(dotNetHelper, _dotNetRef))
            return;
        _dotNetRef = dotNetHelper;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already gone (e.g. browser closed): nothing to clean up JS-side.
            }
            _module = null;
        }

        // The .NET ref is borrowed: it is created and owned by the caller
        // (DashboardHost), which is solely responsible for disposing it.
        // We only hold it to forward into initGrid, so we just drop the reference.
        _dotNetRef = null;
    }
}
