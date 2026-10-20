using BlazorDashboardKit.Services;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorDashboardKit.Tests.Services;

/// <summary>
/// <see cref="DashboardJsInterop"/> is a plain class whose only dependency is
/// <see cref="IJSRuntime"/>, so it is unit-tested with hand-written fakes rather
/// than bunit's <c>TestContext</c> / JS-module-mock machinery (the latter
/// reproducibly hangs the VSTest host inside its module-mock code).
/// </summary>
public class DashboardJsInteropTests
{
    /// <summary>
    /// Records every <c>InvokeAsync</c> / <c>InvokeVoidAsync</c> call. The
    /// <c>InvokeVoidAsync(...)</c> calls in production are extension methods that
    /// funnel into <c>InvokeAsync&lt;IJSVoidResult&gt;</c>, so the recorded
    /// identifier is e.g. <c>"initGrid"</c>.
    /// </summary>
    private sealed class FakeJsObjectReference : IJSObjectReference
    {
        public List<(string Identifier, object?[] Args)> Invocations { get; } = new();
        /// <summary>Return values by identifier; anything not listed returns <c>default</c>.</summary>
        public Dictionary<string, object?> Results { get; } = new();
        public int DisposeCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args ?? Array.Empty<object?>()));
            return Results.TryGetValue(identifier, out var result)
                ? new ValueTask<TValue>((TValue)result!)
                : new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Records every <c>(identifier, args)</c>. For <c>"import"</c> it returns the
    /// shared <see cref="FakeJsObjectReference"/> so the production code can invoke
    /// the module; any other identifier returns <c>default</c>.
    /// </summary>
    private sealed class FakeJsRuntime : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Invocations { get; } = new();
        public FakeJsObjectReference Module { get; } = new();

        public string? ImportPath =>
            Invocations
                .Where(i => i.Identifier == "import")
                .SelectMany(i => i.Args)
                .OfType<string>()
                .FirstOrDefault();

        private ValueTask<TValue> Record<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add((identifier, args ?? Array.Empty<object?>()));
            if (identifier == "import")
                return new ValueTask<TValue>((TValue)(object)Module);
            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => Record<TValue>(identifier, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => Record<TValue>(identifier, args);
    }

    [Fact]
    public async Task InitGrid_NoOps_When_Not_Interactive()
    {
        var js = new FakeJsRuntime();
        var sut = new DashboardJsInterop(js);

        await sut.InitGridAsync("grid-el", interactive: false, default);

        Assert.Empty(js.Invocations);            // no "import"
        Assert.Empty(js.Module.Invocations);     // no "initGrid"
    }

    [Fact]
    public async Task InitGrid_Imports_Module_Then_Calls_initGrid_When_Interactive()
    {
        var js = new FakeJsRuntime();
        var sut = new DashboardJsInterop(js);

        await sut.InitGridAsync("grid-el", interactive: true, default);

        var import = Assert.Single(js.Invocations.Where(i => i.Identifier == "import"));
        Assert.Contains("./_content/BlazorDashboardKit/dashboard-interop.js", import.Args);

        var initGrid = Assert.Single(js.Module.Invocations.Where(i => i.Identifier == "initGrid"));
        Assert.Equal("grid-el", initGrid.Args[0]);
    }

    [Fact]
    public async Task Module_Methods_Are_Safe_NoOps_Before_InitGrid()
    {
        // Plausible ordering: ExitEditMode/Done or Export fires before the grid
        // was ever initialized. With _module still null these must no-op, never
        // NRE on the module or eagerly import it.
        var js = new FakeJsRuntime();
        var sut = new DashboardJsInterop(js);

        await sut.SetEditModeAsync(true, interactive: true, default);
        await sut.SetWidgetLockedAsync("w1", true, interactive: true, default);
        await sut.DestroyGridAsync(interactive: true, default);
        await sut.AddGridWidgetAsync(default, interactive: true, default);
        await sut.RemoveGridWidgetAsync(default, interactive: true, default);
        await sut.DownloadJsonAsync("f.json", "{}", interactive: true, default);
        await sut.CopyToClipboardAsync("x", interactive: true, default);

        Assert.Empty(js.Invocations);          // never imported the module
        Assert.Empty(js.Module.Invocations);   // never invoked anything on it
    }

    [Fact]
    public async Task SetEditMode_Returns_The_Compacted_Layout_From_The_Module()
    {
        var js = new FakeJsRuntime();
        js.Module.Results["setEditMode"] = "[]";
        var sut = new DashboardJsInterop(js);
        await sut.InitGridAsync("grid-el", interactive: true, default);

        var layout = await sut.SetEditModeAsync(false, interactive: true, default);

        Assert.Equal("[]", layout);
        Assert.Null(await sut.SetEditModeAsync(false, interactive: false, default));
    }

    [Fact]
    public async Task SetWidgetLocked_Passes_The_Widget_Id_And_Lock_State()
    {
        var js = new FakeJsRuntime();
        var sut = new DashboardJsInterop(js);
        await sut.InitGridAsync("grid-el", interactive: true, default);

        await sut.SetWidgetLockedAsync("w1", true, interactive: true, default);

        var call = Assert.Single(js.Module.Invocations.Where(i => i.Identifier == "setWidgetLocked"));
        Assert.Equal(new object?[] { "w1", true }, call.Args);
    }

    [Fact]
    public async Task Dispose_Is_Idempotent_And_NoThrow()
    {
        var js = new FakeJsRuntime();
        var sut = new DashboardJsInterop(js);
        await sut.InitGridAsync("grid-el", interactive: true, default);

        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }
}
