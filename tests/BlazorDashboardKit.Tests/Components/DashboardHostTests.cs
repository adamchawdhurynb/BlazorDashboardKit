using AngleSharp.Dom;
using BlazorDashboardKit.Abstractions;
using BlazorDashboardKit.Models;
using BlazorDashboardKit.Services;
using BlazorDashboardKit.Stores;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorDashboardKit.Tests.Components;

public class DashboardHostTests : TestContext
{
    [Fact]
    public async Task Loads_Persisted_Dashboard_And_Flags_Unknown_Widget()
    {
        var store = new InMemoryDashboardStore();
        var c = new DashboardCollection { ActiveDashboardId = "d1" };
        c.Dashboards.Add(new DashboardDefinition { Id = "d1",
            Widgets = { new WidgetPlacement { WidgetType = "Ghost" } } });
        await store.SaveAsync("owner-1", c);

        // Loose JS so the host's interactive grid-init (bunit invokes
        // OnAfterRenderAsync, which flips the net8 interactivity gate) resolves to
        // inert no-ops instead of throwing. We never call JSInterop.SetupModule,
        // so bunit's module-mock machinery (the documented VSTest-host hang) is
        // never engaged; the assertion below only inspects the synchronous static
        // markup, which renders before OnAfterRenderAsync.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped<DashboardJsInterop>(_ => new DashboardJsInterop(JSInterop.JSRuntime));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1"));
        cut.WaitForState(() => cut.Markup.Contains("widget-unavailable"), TimeSpan.FromSeconds(5));
        Assert.Contains("widget-unavailable", cut.Markup);
    }

    [Fact]
    public void Actions_Menu_Toggles_Open_Without_Bootstrap_Js()
    {
        // The Actions dropdown must open via Blazor state, not Bootstrap's JS
        // bundle (which the kit deliberately does not ship or require).
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped<DashboardJsInterop>(_ => new DashboardJsInterop(JSInterop.JSRuntime));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(
            () => cut.FindAll("button.btn-outline-secondary.dropdown-toggle").Count == 1,
            TimeSpan.FromSeconds(5));

        IElement ActionsMenu() => cut
            .Find("button.btn-outline-secondary.dropdown-toggle")
            .ParentElement!
            .QuerySelector(".dropdown-menu")!;

        Assert.DoesNotContain("show", ActionsMenu().ClassList);

        cut.Find("button.btn-outline-secondary.dropdown-toggle").Click();
        Assert.Contains("show", ActionsMenu().ClassList);

        cut.Find("button.btn-outline-secondary.dropdown-toggle").Click();
        Assert.DoesNotContain("show", ActionsMenu().ClassList);
    }

    [Fact]
    public void Actions_Menu_Closes_On_Click_Outside_Backdrop()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped<DashboardJsInterop>(_ => new DashboardJsInterop(JSInterop.JSRuntime));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(
            () => cut.FindAll("button.btn-outline-secondary.dropdown-toggle").Count == 1,
            TimeSpan.FromSeconds(5));

        IElement ActionsMenu() => cut
            .Find("button.btn-outline-secondary.dropdown-toggle")
            .ParentElement!.QuerySelector(".dropdown-menu")!;

        cut.Find("button.btn-outline-secondary.dropdown-toggle").Click();
        Assert.Contains("show", ActionsMenu().ClassList);

        cut.Find(".bdk-dropdown-backdrop").Click();
        Assert.DoesNotContain("show", ActionsMenu().ClassList);
    }

    [Fact]
    public void Actions_Menu_Closes_After_Choosing_Export()
    {
        // Parity with the widget picker: choosing an item dismisses the menu,
        // the way Bootstrap's JS used to.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped<DashboardJsInterop>(_ => new DashboardJsInterop(JSInterop.JSRuntime));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(
            () => cut.FindAll("button.btn-outline-secondary.dropdown-toggle").Count == 1,
            TimeSpan.FromSeconds(5));

        IElement ActionsMenu() => cut
            .Find("button.btn-outline-secondary.dropdown-toggle")
            .ParentElement!
            .QuerySelector(".dropdown-menu")!;

        cut.Find("button.btn-outline-secondary.dropdown-toggle").Click();
        Assert.Contains("show", ActionsMenu().ClassList);

        cut.Find(".dropdown-menu .dropdown-item").Click(); // "Export"
        Assert.DoesNotContain("show", ActionsMenu().ClassList);
    }

    [Fact]
    public void Empty_OwnerKey_Renders_Inert_Container_And_Never_Touches_Store()
    {
        // An empty OwnerKey means "no owner": the host must render the inert
        // no-owner branch without ever resolving/saving against the store.
        // ThrowingStore turns any store access into a hard test failure.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IDashboardStore>(new ThrowingStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped<DashboardJsInterop>(_ => new DashboardJsInterop(JSInterop.JSRuntime));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, ""));

        Assert.Contains("dashboard-empty-container", cut.Markup);
        Assert.DoesNotContain("dashboard-header", cut.Markup);
    }

    [Fact]
    public void Grid_Is_Reinitialized_After_Adding_A_Widget()
    {
        // Repro: AddWidget -> ResetGrid() destroys the GridStack instance and sets
        // _gridInitialized=false, expecting the next render to re-init. The sole
        // InitGrid call site is gated on OnAfterRenderAsync's `firstRender`, which
        // is true exactly once. So a widget added after first render is never
        // adopted by GridStack and collapses to 0x0 at position 0,0. The grid
        // MUST re-initialize after the widget set changes.
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test",
            Name = "Test Widget",
            Category = "Demo",
            ComponentType = typeof(TestWidget)
        };

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        cut.Find("button.btn-outline-primary.dropdown-toggle").Click(); // open picker
        cut.Find("button.dropdown-item.small").Click();                  // add the widget

        cut.WaitForState(() => js.InitGridCalls >= 2, TimeSpan.FromSeconds(5));
        Assert.Equal(2, js.InitGridCalls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Widget_Debug_Label_Is_Opt_In(bool showDebugInfo, bool expectLabel)
    {
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test Widget", Category = "Demo",
            ComponentType = typeof(TestWidget)
        };

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true)
            .Add(x => x.ShowDebugInfo, showDebugInfo));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        cut.Find("button.btn-outline-primary.dropdown-toggle").Click();
        cut.Find("button.dropdown-item.small").Click();
        cut.WaitForState(() => cut.Markup.Contains("test-widget-body"), TimeSpan.FromSeconds(5));

        Assert.Equal(expectLabel, cut.Markup.Contains("widget-debug-label"));
    }

    [Fact]
    public async Task Multiple_Dashboards_Can_Be_Added_Switched_And_Deleted()
    {
        var js = new RecordingJsRuntime();
        var store = new InMemoryDashboardStore();
        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        // One default dashboard: no selector yet, but the add button exists.
        Assert.Empty(cut.FindAll("select[aria-label='Select dashboard']"));
        cut.Find("button[title='New dashboard']").Click();

        // Two dashboards now: a selector with 2 options, the new one active.
        cut.WaitForState(
            () => cut.FindAll("select[aria-label='Select dashboard'] option").Count == 2,
            TimeSpan.FromSeconds(5));
        var stored = await store.LoadAsync("owner-1");
        Assert.Equal(2, stored!.Dashboards.Count);
        Assert.Equal(stored.Dashboards[1].Id, stored.ActiveDashboardId);
        var firstId = stored.Dashboards[0].Id;
        var firstName = stored.Dashboards[0].Name;

        // Newly-added dashboard is active (its name shows in the rename box).
        Assert.Equal("Dashboard 2", cut.Find("input[aria-label='Dashboard name']").GetAttribute("value"));

        // Switch back to the first dashboard (UI reflects the active one).
        cut.Find("select[aria-label='Select dashboard']").Change(firstId);
        cut.WaitForState(
            () => cut.Find("input[aria-label='Dashboard name']").GetAttribute("value") == firstName,
            TimeSpan.FromSeconds(5));

        // Delete the active dashboard (2-step confirm) -> back to one, no selector.
        cut.Find("button[title='Delete this dashboard']").Click();
        cut.Find("button[title='Confirm delete dashboard']").Click();
        cut.WaitForState(() => cut.FindAll("select[aria-label='Select dashboard']").Count == 0,
            TimeSpan.FromSeconds(5));
        var afterDelete = await store.LoadAsync("owner-1");
        Assert.Single(afterDelete!.Dashboards);
    }

    [Fact]
    public void OnDashboardChanged_Fires_After_A_Persisted_Change()
    {
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test Widget", Category = "Demo",
            ComponentType = typeof(TestWidget)
        };
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var changes = 0;
        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true)
            .Add(x => x.OnDashboardChanged, () => changes++));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        cut.Find("button.btn-outline-primary.dropdown-toggle").Click();
        cut.Find("button.dropdown-item.small").Click();
        cut.WaitForState(() => changes > 0, TimeSpan.FromSeconds(5));

        Assert.True(changes > 0);
    }

    [Fact]
    public void Duplicating_A_Widget_Adds_An_Independent_Copy()
    {
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test Widget", Category = "Demo",
            ComponentType = typeof(TestWidget)
        };
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        cut.Find("button.btn-outline-primary.dropdown-toggle").Click();
        cut.Find("button.dropdown-item.small").Click();
        cut.WaitForState(() => cut.FindAll(".grid-stack-item").Count == 1, TimeSpan.FromSeconds(5));

        cut.Find("button[title='Duplicate widget']").Click();
        cut.WaitForState(() => cut.FindAll(".grid-stack-item").Count == 2, TimeSpan.FromSeconds(5));

        var ids = cut.FindAll(".grid-stack-item").Select(e => e.GetAttribute("gs-id")).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(2, ids.Distinct().Count());                 // distinct placement ids
        Assert.Equal(2, cut.FindAll(".test-widget-body").Count);  // both render the same widget
    }

    [Fact]
    public void Locking_A_Widget_Emits_GridStack_Lock_Attributes()
    {
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test Widget", Category = "Demo",
            ComponentType = typeof(TestWidget)
        };
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        cut.Find("button.btn-outline-primary.dropdown-toggle").Click();
        cut.Find("button.dropdown-item.small").Click();
        cut.WaitForState(() => cut.FindAll(".grid-stack-item").Count == 1, TimeSpan.FromSeconds(5));

        Assert.Null(cut.Find(".grid-stack-item").GetAttribute("gs-locked"));

        cut.Find("button[title='Lock widget']").Click();
        cut.WaitForState(
            () => cut.Find(".grid-stack-item").GetAttribute("gs-locked") == "true",
            TimeSpan.FromSeconds(5));

        var item = cut.Find(".grid-stack-item");
        Assert.Equal("true", item.GetAttribute("gs-no-move"));
        Assert.Equal("true", item.GetAttribute("gs-no-resize"));
        Assert.NotEmpty(cut.FindAll("button[title='Unlock widget']")); // toggled
    }

    [Fact]
    public async Task Locking_A_Widget_Updates_It_In_Place_Without_Rebuilding_The_Grid()
    {
        // A rebuild flashed every widget through the static fallback layout; the lock
        // must reach GridStack for the one widget only, and still be persisted.
        var store = new InMemoryDashboardStore();
        var js = new RecordingJsRuntime();
        RenderHostWithSeededWidgets(store, js, out var cut);

        cut.FindAll("button[title='Lock widget']")[0].Click();
        cut.WaitForState(() => js.ModuleCalls.Any(c => c.Identifier == "setWidgetLocked"),
            TimeSpan.FromSeconds(5));

        var call = Assert.Single(js.ModuleCalls, c => c.Identifier == "setWidgetLocked");
        Assert.Equal(new object?[] { "w1", true }, call.Args);
        Assert.Equal(1, js.InitGridCalls);                                     // no rebuild
        Assert.DoesNotContain(js.ModuleCalls, c => c.Identifier == "destroyGrid");
        Assert.True((await store.LoadAsync("owner-1"))!.Dashboards[0].Widgets.First(w => w.Id == "w1").Locked);
    }

    [Fact]
    public void Custom_GridOptions_Are_Forwarded_To_The_Grid_Init()
    {
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var opts = new DashboardGridOptions { Columns = 6, CellHeight = 100, Margin = 4, MobileColumns = 2 };
        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true)
            .Add(x => x.GridOptions, opts));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        Assert.NotNull(js.LastGridOptions);
        Assert.Equal(6, js.LastGridOptions!.Columns);
        Assert.Equal(100, js.LastGridOptions.CellHeight);
        Assert.Equal(4, js.LastGridOptions.Margin);
        Assert.Equal(2, js.LastGridOptions.MobileColumns);
    }

    [Fact]
    public void Default_GridOptions_Are_Sent_When_None_Provided()
    {
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        Assert.NotNull(js.LastGridOptions);
        Assert.Equal(12, js.LastGridOptions!.Columns);   // kit default
        Assert.Equal(146, js.LastGridOptions.CellHeight);
    }

    [Fact]
    public void Widget_Size_Constraints_From_Descriptor_Reach_The_Grid()
    {
        // The descriptor declares Min/Max column/row sizes; they must be emitted
        // as GridStack gs-min/max attributes or resize is unconstrained.
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test Widget", Category = "Demo",
            ComponentType = typeof(TestWidget),
            MinColumnSize = 2, MaxColumnSize = 8,
            MinRowSpan = 1, MaxRowSpan = 5
        };

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        cut.Find("button.btn-outline-primary.dropdown-toggle").Click();
        cut.Find("button.dropdown-item.small").Click();
        cut.WaitForState(() => cut.FindAll(".grid-stack-item").Count == 1, TimeSpan.FromSeconds(5));

        var item = cut.Find(".grid-stack-item");
        Assert.Equal("2", item.GetAttribute("gs-min-w"));
        Assert.Equal("8", item.GetAttribute("gs-max-w"));
        Assert.Equal("1", item.GetAttribute("gs-min-h"));
        Assert.Equal("5", item.GetAttribute("gs-max-h"));
    }

    private BlazorDashboardKit.Components.DashboardHost RenderHostWithSeededWidgets(
        InMemoryDashboardStore store, RecordingJsRuntime js,
        out IRenderedComponent<BlazorDashboardKit.Components.DashboardHost> cut)
    {
        var c = new DashboardCollection { ActiveDashboardId = "d1" };
        c.Dashboards.Add(new DashboardDefinition
        {
            Id = "d1",
            Widgets =
            {
                new WidgetPlacement { Id = "w1", WidgetType = "Test", ColumnSize = 2, RowSpan = 2 },
                new WidgetPlacement { Id = "w2", WidgetType = "Test", ColumnSize = 3, RowSpan = 1 }
            }
        });
        store.SaveAsync("owner-1", c).GetAwaiter().GetResult();

        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[]
            { new WidgetDescriptor { Type = "Test", Name = "Test", Category = "T", ComponentType = typeof(TestWidget) } }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1").Add(x => x.EditMode, true));
        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        return cut.Instance;
    }

    private IRenderedComponent<BlazorDashboardKit.Components.DashboardHost> RenderWithPlacements(
        params WidgetPlacement[] placements)
    {
        var store = new InMemoryDashboardStore();
        var c = new DashboardCollection { ActiveDashboardId = "d1" };
        var dash = new DashboardDefinition { Id = "d1" };
        foreach (var p in placements) { p.WidgetType = "Test"; dash.Widgets.Add(p); }
        c.Dashboards.Add(dash);
        store.SaveAsync("owner-1", c).GetAwaiter().GetResult();

        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[]
            { new WidgetDescriptor { Type = "Test", Name = "Test", Category = "T", ComponentType = typeof(TestWidget) } }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1").Add(x => x.EditMode, true));
        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        return cut;
    }

    private static (string x, string y, string w) Pos(IRenderedComponent<BlazorDashboardKit.Components.DashboardHost> cut, string id)
    {
        var el = cut.FindAll(".grid-stack-item").First(e => e.GetAttribute("gs-id") == id);
        return (el.GetAttribute("gs-x")!, el.GetAttribute("gs-y")!, el.GetAttribute("gs-w")!);
    }

    [Fact]
    public void ComputeGridPositions_Renders_Explicit_Coordinates_Verbatim()
    {
        var cut = RenderWithPlacements(
            new WidgetPlacement { Id = "e1", Offset = 4, Row = 3, ColumnSize = 5, RowSpan = 2 });
        Assert.Equal(("4", "3", "5"), Pos(cut, "e1"));
    }

    [Fact]
    public void ComputeGridPositions_AutoFlows_And_Wraps_At_12_Columns()
    {
        // a(8 wide) then b(6 wide): 8+6 > 12 -> b wraps to x=0, y = a.RowSpan.
        var cut = RenderWithPlacements(
            new WidgetPlacement { Id = "a", Order = 0, ColumnSize = 8, RowSpan = 2 },
            new WidgetPlacement { Id = "b", Order = 1, ColumnSize = 6, RowSpan = 1 });
        Assert.Equal(("0", "0", "8"), Pos(cut, "a"));
        Assert.Equal(("0", "2", "6"), Pos(cut, "b"));   // wrapped below a (a.RowSpan = 2)
    }

    [Fact]
    public void ComputeGridPositions_AutoFlow_Starts_Below_The_Lowest_Explicit_Row()
    {
        // explicit occupies rows 2..3 (Row 2 + RowSpan 2) -> auto-flow starts at y=4.
        var cut = RenderWithPlacements(
            new WidgetPlacement { Id = "exp", Offset = 0, Row = 2, ColumnSize = 4, RowSpan = 2 },
            new WidgetPlacement { Id = "auto", Order = 0, ColumnSize = 3, RowSpan = 1 });
        Assert.Equal(("0", "2", "4"), Pos(cut, "exp"));
        Assert.Equal(("0", "4", "3"), Pos(cut, "auto"));
    }

    [Fact]
    public async Task OnGridChanged_Applies_Valid_Changes_And_Persists()
    {
        var store = new InMemoryDashboardStore();
        var host = RenderHostWithSeededWidgets(store, new RecordingJsRuntime(), out var cut);

        await cut.InvokeAsync(() => host.OnGridChanged(
            """[{"id":"w1","x":3,"y":5,"w":4,"h":2}]"""));

        var w1 = (await store.LoadAsync("owner-1"))!.Dashboards[0].Widgets.First(w => w.Id == "w1");
        Assert.Equal(3, w1.Offset);
        Assert.Equal(5, w1.Row);
        Assert.Equal(5, w1.Order);
        Assert.Equal(4, w1.ColumnSize);
        Assert.Equal(2, w1.RowSpan);
    }

    [Fact]
    public async Task Done_Saves_The_Layout_The_Grid_Compacted_To()
    {
        // Leaving edit mode compacts the grid in JS, which returns the new layout;
        // the host applies it and saves once.
        var store = new InMemoryDashboardStore();
        var js = new RecordingJsRuntime
        {
            SetEditModeResult = """[{"id":"w1","x":0,"y":0,"w":2,"h":2},{"id":"w2","x":2,"y":0,"w":3,"h":1}]"""
        };
        RenderHostWithSeededWidgets(store, js, out var cut);

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Done").Click();
        cut.WaitForState(() => cut.FindAll("button").Any(b => b.TextContent.Trim() == "Edit"),
            TimeSpan.FromSeconds(5));

        var setEditMode = Assert.Single(js.ModuleCalls, c => c.Identifier == "setEditMode");
        Assert.Equal(new object?[] { false }, setEditMode.Args);
        var d = (await store.LoadAsync("owner-1"))!.Dashboards[0];
        var w2 = d.Widgets.First(w => w.Id == "w2");
        Assert.Equal(2, w2.Offset);
        Assert.Equal(0, w2.Row);
        Assert.Equal(0, d.Widgets.First(w => w.Id == "w1").Offset);
    }

    [Fact]
    public async Task OnGridChanged_Ignores_Malformed_Json_Without_Throwing()
    {
        var store = new InMemoryDashboardStore();
        var host = RenderHostWithSeededWidgets(store, new RecordingJsRuntime(), out var cut);

        var ex = await Record.ExceptionAsync(() =>
            cut.InvokeAsync(() => host.OnGridChanged("{ not valid json")));
        Assert.Null(ex);

        // Untouched: w1 still at its seeded size, no explicit Offset.
        var w1 = (await store.LoadAsync("owner-1"))!.Dashboards[0].Widgets.First(w => w.Id == "w1");
        Assert.Null(w1.Offset);
        Assert.Equal(2, w1.ColumnSize);
    }

    [Fact]
    public async Task OnGridChanged_Skips_Unknown_Ids_But_Applies_The_Rest()
    {
        var store = new InMemoryDashboardStore();
        var host = RenderHostWithSeededWidgets(store, new RecordingJsRuntime(), out var cut);

        await cut.InvokeAsync(() => host.OnGridChanged(
            """[{"id":"ghost","x":1,"y":1,"w":1,"h":1},{"id":"w2","x":0,"y":7,"w":6,"h":3}]"""));

        var d = (await store.LoadAsync("owner-1"))!.Dashboards[0];
        Assert.DoesNotContain(d.Widgets, w => w.Id == "ghost");
        var w2 = d.Widgets.First(w => w.Id == "w2");
        Assert.Equal(6, w2.ColumnSize);
        Assert.Equal(7, w2.Row);
    }

    [Fact]
    public void WidgetPickerTemplate_Override_Replaces_The_Default_Picker_And_Add_Flows()
    {
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test", Category = "T", ComponentType = typeof(TestWidget)
        };
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        RenderFragment<BlazorDashboardKit.Components.WidgetPickerContext> tpl = ctx => b =>
        {
            b.OpenElement(0, "button");
            b.AddAttribute(1, "class", "my-add");
            b.AddAttribute(2, "onclick",
                EventCallback.Factory.Create(this, () => ctx.Add.InvokeAsync(ctx.Available[0])));
            b.AddContent(3, $"add {ctx.Available.Count}");
            b.CloseElement();
        };

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true)
            .Add(x => x.WidgetPickerTemplate, tpl));
        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll("button.my-add"));                          // override rendered
        Assert.Empty(cut.FindAll("button.btn-outline-primary.dropdown-toggle")); // default picker gone
        Assert.Contains("add 1", cut.Markup);                                   // access-filtered list flowed in

        cut.Find("button.my-add").Click();
        cut.WaitForState(() => cut.FindAll(".grid-stack-item").Count == 1, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ToolbarTemplate_Override_Replaces_The_Default_Toolbar_And_Actions_Flow()
    {
        var store = new InMemoryDashboardStore();
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[]
            { new WidgetDescriptor { Type = "Test", Name = "Test", Category = "T", ComponentType = typeof(TestWidget) } }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        BlazorDashboardKit.Components.DashboardToolbarContext? ctx = null;
        RenderFragment<BlazorDashboardKit.Components.DashboardToolbarContext> tpl = c => b =>
        {
            ctx = c;
            b.OpenElement(0, "div");
            b.AddAttribute(1, "class", "my-toolbar");
            b.AddContent(2, c.Active.Name);
            b.CloseElement();
        };

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.ToolbarTemplate, tpl));
        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll(".my-toolbar"));          // override rendered
        Assert.Empty(cut.FindAll(".dashboard-header"));       // default toolbar gone
        Assert.False(ctx!.EditMode);
        Assert.False(ctx.CanDelete);                          // only one dashboard

        await cut.InvokeAsync(() => ctx.EnterEditMode.InvokeAsync());
        Assert.True(ctx.EditMode);
        Assert.Contains(js.ModuleCalls, c => c.Identifier == "setEditMode" && Equals(c.Args[0], true));

        await cut.InvokeAsync(() => ctx.AddWidget.InvokeAsync(ctx.AvailableWidgets[0]));
        cut.WaitForState(() => cut.FindAll(".grid-stack-item").Count == 1, TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => ctx.Rename.InvokeAsync("  Renamed  "));
        Assert.Equal("Renamed", ctx.Active.Name);

        await cut.InvokeAsync(() => ctx.AddDashboard.InvokeAsync());
        Assert.Equal(2, ctx.Dashboards.Count);
        Assert.True(ctx.CanDelete);

        var first = ctx.Dashboards[0];
        await cut.InvokeAsync(() => ctx.Switch.InvokeAsync(first.Id));
        Assert.Same(first, ctx.Active);

        await cut.InvokeAsync(() => ctx.DeleteActive.InvokeAsync());
        Assert.Single(ctx.Dashboards);

        await cut.InvokeAsync(() => ctx.ExitEditMode.InvokeAsync());
        Assert.False(ctx.EditMode);

        var saved = (await store.LoadAsync("owner-1"))!;
        Assert.Single(saved.Dashboards);
        Assert.NotEqual(first.Id, saved.Dashboards[0].Id);   // the active (first) one was deleted
    }

    [Fact]
    public async Task MaxDashboardNameLength_Cuts_Renamed_And_Imported_Names()
    {
        var store = new InMemoryDashboardStore();
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        BlazorDashboardKit.Components.DashboardToolbarContext? ctx = null;
        RenderFragment<BlazorDashboardKit.Components.DashboardToolbarContext> tpl = c => b => ctx = c;

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.MaxDashboardNameLength, 10)
            .Add(x => x.ToolbarTemplate, tpl));
        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        Assert.Equal(10, ctx!.MaxNameLength);

        // Cut to 10, then the trailing space the cut leaves is trimmed too.
        await cut.InvokeAsync(() => ctx.Rename.InvokeAsync("  Quarterly results  "));
        Assert.Equal("Quarterly", ctx.Active.Name);

        await cut.InvokeAsync(() => ctx.Import.InvokeAsync(
            """{"Name":"An imported dashboard","Widgets":[]}"""));
        Assert.Equal("An importe", ctx.Active.Name);

        // A name that trims to nothing falls back to the default.
        await cut.InvokeAsync(() => ctx.Import.InvokeAsync("""{"Name":"   ","Widgets":[]}"""));
        Assert.Equal("Imported Dashboard", ctx.Active.Name);

        Assert.Equal(ctx.Active.Name, (await store.LoadAsync("owner-1"))!.Dashboards[0].Name);
    }

    [Fact]
    public void ToolbarComponent_Override_Replaces_The_Default_Toolbar_And_Actions_Flow()
    {
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.ToolbarComponent, typeof(TestToolbarComponent)));
        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        Assert.Empty(cut.FindAll(".dashboard-header"));
        Assert.Contains(":viewing", cut.Find(".custom-toolbar").TextContent);

        cut.Find("button.custom-edit").Click();
        cut.WaitForState(() => cut.Find(".custom-toolbar").TextContent.Contains(":editing"),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Toolbar_Context_Cannot_Enter_Edit_Mode_When_ReadOnly()
    {
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.ReadOnly, true)
            .Add(x => x.ToolbarComponent, typeof(TestToolbarComponent)));
        cut.WaitForState(() => cut.FindAll(".custom-toolbar").Count == 1, TimeSpan.FromSeconds(5));

        cut.Find("button.custom-edit").Click();

        Assert.Contains(":viewing", cut.Find(".custom-toolbar").TextContent);
        Assert.DoesNotContain(js.ModuleCalls, c => c.Identifier == "setEditMode");
    }

    [Fact]
    public void EmptyTemplate_Override_Replaces_The_Default_Empty_State()
    {
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>()));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        RenderFragment<BlazorDashboardKit.Components.EmptyDashboardContext> tpl = ctx => b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "class", "my-empty");
            b.AddContent(2, ctx.EditMode ? "edit-empty" : "view-empty");
            b.CloseElement();
        };

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true)
            .Add(x => x.EmptyTemplate, tpl));

        Assert.NotEmpty(cut.FindAll(".my-empty"));
        Assert.Contains("edit-empty", cut.Markup);
        Assert.DoesNotContain("This dashboard is empty", cut.Markup);
    }

    [Fact]
    public async Task WidgetUnavailableTemplate_Override_Replaces_The_Default_Placeholder()
    {
        var store = new InMemoryDashboardStore();
        var c = new DashboardCollection { ActiveDashboardId = "d1" };
        c.Dashboards.Add(new DashboardDefinition
        {
            Id = "d1",
            Widgets = { new WidgetPlacement { Id = "g", WidgetType = "Ghost" } }
        });
        await store.SaveAsync("owner-1", c);

        var js = new RecordingJsRuntime();
        Services.AddSingleton<IDashboardStore>(store);
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(Array.Empty<WidgetDescriptor>())); // "Ghost" unknown
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        RenderFragment<BlazorDashboardKit.Components.WidgetUnavailableContext> tpl = ctx => b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "class", "my-unavailable");
            b.AddContent(2, ctx.Placement.WidgetType);
            b.CloseElement();
        };

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true)
            .Add(x => x.WidgetUnavailableTemplate, tpl));
        cut.WaitForState(() => cut.Markup.Contains("my-unavailable"), TimeSpan.FromSeconds(5));

        Assert.Contains("Ghost", cut.Find(".my-unavailable").TextContent);
        Assert.DoesNotContain("widget-unavailable", cut.Markup);
    }

    [Fact]
    public void Grid_Container_Class_Is_Never_Changed_By_A_Render()
    {
        // GridStack adds its own classes to the container (grid-stack-animate, the
        // column class, ...). A re-render that changed the class attribute would wipe
        // them, so the markup's class stays fixed and dashboard-interop.js owns the two
        // classes that change: it removes `bdk-grid-static` once the grid is live and
        // switches `dashboard-edit`.
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test", Name = "Test Widget", Category = "Demo",
            ComponentType = typeof(TestWidget)
        };

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));
        Assert.Equal("grid-stack bdk-grid-static", cut.Find(".grid-stack").GetAttribute("class"));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Done").Click();
        cut.WaitForState(() => cut.FindAll("button").Any(b => b.TextContent.Trim() == "Edit"),
            TimeSpan.FromSeconds(5));
        Assert.Equal("grid-stack bdk-grid-static", cut.Find(".grid-stack").GetAttribute("class"));
    }

    [Fact]
    public void Grid_Is_Reinitialized_After_Removing_A_Widget()
    {
        // AddWidget ends with ResetGrid() so the grid rebuilds; RemoveWidget must
        // do the same. Otherwise GridStack keeps a stale internal model that still
        // references the removed DOM node and the remaining widgets are not relaid.
        var js = new RecordingJsRuntime();
        var descriptor = new WidgetDescriptor
        {
            Type = "Test",
            Name = "Test Widget",
            Category = "Demo",
            ComponentType = typeof(TestWidget)
        };

        Services.AddSingleton<IDashboardStore>(new InMemoryDashboardStore());
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        Services.AddSingleton(new WidgetRegistry(new[] { descriptor }));
        Services.AddScoped<DashboardService>();
        Services.AddScoped(_ => new DashboardJsInterop(js));

        var cut = RenderComponent<BlazorDashboardKit.Components.DashboardHost>(p => p
            .Add(x => x.OwnerKey, "owner-1")
            .Add(x => x.EditMode, true));

        cut.WaitForState(() => js.InitGridCalls == 1, TimeSpan.FromSeconds(5));

        cut.Find("button.btn-outline-primary.dropdown-toggle").Click(); // open picker
        cut.Find("button.dropdown-item.small").Click();                  // add widget
        cut.WaitForState(() => js.InitGridCalls == 2, TimeSpan.FromSeconds(5));

        cut.Find("button[title='Remove widget']").Click();               // 1st: arm confirm
        cut.Find("button[title='Confirm remove']").Click();              // 2nd: actually remove
        cut.WaitForState(() => js.InitGridCalls >= 3, TimeSpan.FromSeconds(5));
        Assert.Equal(3, js.InitGridCalls);
    }

    /// <summary>
    /// Minimal <see cref="IJSRuntime"/> that resolves the interop ESM module to a
    /// recording stand-in, letting the test count how many times the host asks the
    /// module to run <c>initGrid</c> without engaging bunit's module-mock machinery.
    /// </summary>
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        private readonly RecordingModule _module = new();
        public int InitGridCalls => _module.InitGridCalls;
        public DashboardGridOptions? LastGridOptions => _module.LastGridOptions;
        /// <summary>Every module call after the import, in order.</summary>
        public List<(string Identifier, object?[] Args)> ModuleCalls => _module.Calls;
        /// <summary>What setEditMode returns: the compacted layout JSON when leaving edit mode.</summary>
        public string? SetEditModeResult { set => _module.SetEditModeResult = value; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            // DashboardJsInterop.InitGridAsync does: _js.InvokeAsync<IJSObjectReference>("import", ...)
            if (identifier == "import")
                return new ValueTask<TValue>((TValue)(object)_module);
            return new ValueTask<TValue>(default(TValue)!);
        }

        private sealed class RecordingModule : IJSObjectReference
        {
            public int InitGridCalls;
            public DashboardGridOptions? LastGridOptions;
            public readonly List<(string Identifier, object?[] Args)> Calls = new();
            public string? SetEditModeResult;

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
                => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(
                string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                Calls.Add((identifier, args ?? Array.Empty<object?>()));
                if (identifier == "initGrid")
                {
                    InitGridCalls++;
                    // initGrid args: [containerId, dotNetRef, editMode, DashboardGridOptions]
                    LastGridOptions = args?.OfType<DashboardGridOptions>().FirstOrDefault();
                }
                if (identifier == "setEditMode" && SetEditModeResult is not null && typeof(TValue) == typeof(string))
                    return new ValueTask<TValue>((TValue)(object)SetEditModeResult);
                return new ValueTask<TValue>(default(TValue)!);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Store whose every operation throws, so a single store access fails the test.
    /// </summary>
    private sealed class ThrowingStore : IDashboardStore
    {
        public Task<DashboardCollection?> LoadAsync(string ownerKey, CancellationToken ct = default)
            => throw new InvalidOperationException("Store must not be accessed for an empty OwnerKey.");

        public Task SaveAsync(string ownerKey, DashboardCollection collection, CancellationToken ct = default)
            => throw new InvalidOperationException("Store must not be accessed for an empty OwnerKey.");
    }
}
