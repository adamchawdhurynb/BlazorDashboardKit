using BlazorDashboardKit.Abstractions;
using BlazorDashboardKit.Models;
using BlazorDashboardKit.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorDashboardKit.Tests.Components;

public class WidgetContainerTests : TestContext
{
    private WidgetDescriptor Probe(bool alwaysInteractive) => new()
    {
        Type = "Probe",
        Name = "Probe",
        Category = "Test",
        ComponentType = typeof(ReadonlyProbeWidget),
        AlwaysInteractive = alwaysInteractive
    };

    private IRenderedComponent<BlazorDashboardKit.Components.WidgetContainer> Render(
        WidgetDescriptor descriptor, bool editMode, bool readOnly, bool hostInteractive)
    {
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        return RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Probe" })
            .Add(x => x.Descriptor, descriptor)
            .Add(x => x.EditMode, editMode)
            .Add(x => x.ReadOnly, readOnly)
            .Add(x => x.HostInteractive, hostInteractive));
    }

    [Fact]
    public void Default_Header_Controls_Are_Visible_Without_An_Icon_Font()
    {
        // The kit ships no icon font; its own controls must render visible
        // glyphs, not empty icon-font spans (regression: Configure/Remove blank).
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        var cut = RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Probe" })
            .Add(x => x.Descriptor, Probe(false))
            .Add(x => x.EditMode, true));

        Assert.Empty(cut.FindAll("span.icon"));   // no icon-font dependency anywhere
        Assert.Equal("⚙", cut.Find("button[title='Configure widget'] span").TextContent);
        Assert.Equal("✕", cut.Find("button[title='Remove widget'] span").TextContent);
        Assert.Equal("⧉", cut.Find("button[title='Duplicate widget'] span").TextContent);
        Assert.NotEmpty(cut.Find("button[title='Lock widget'] span").TextContent);
    }

    [Fact]
    public void Default_Header_Renders_When_No_Override()
    {
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        var cut = RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Probe" })
            .Add(x => x.Descriptor, Probe(false))
            .Add(x => x.EditMode, true));

        Assert.NotEmpty(cut.FindAll(".widget-edit-header"));
        Assert.NotEmpty(cut.FindAll("button[title='Remove widget']"));
        Assert.Empty(cut.FindAll(".custom-header"));
    }

    [Fact]
    public void Component_Type_Override_Replaces_The_Header_And_Actions_Flow()
    {
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        var removed = 0;
        var cut = RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Probe" })
            .Add(x => x.Descriptor, Probe(false))
            .Add(x => x.EditMode, true)
            .Add(x => x.OnRemove, () => removed++)
            .Add(x => x.HeaderComponent, typeof(TestHeaderComponent)));

        Assert.NotEmpty(cut.FindAll(".custom-header"));        // override rendered
        Assert.Empty(cut.FindAll(".widget-edit-header"));      // default replaced
        Assert.Empty(cut.FindAll("button[title='Remove widget']"));

        cut.Find("button.custom-remove").Click();              // its Remove callback flows
        Assert.Equal(1, removed);
    }

    [Fact]
    public void RenderFragment_Override_Wins_Over_Component_And_Default()
    {
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        var removed = 0;
        RenderFragment<BlazorDashboardKit.Components.WidgetHeaderContext> tpl = ctx => b =>
        {
            b.OpenElement(0, "button");
            b.AddAttribute(1, "class", "frag-remove");
            b.AddAttribute(2, "onclick", ctx.Remove);
            b.AddContent(3, "remove");
            b.CloseElement();
        };

        var cut = RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Probe" })
            .Add(x => x.Descriptor, Probe(false))
            .Add(x => x.EditMode, true)
            .Add(x => x.OnRemove, () => removed++)
            .Add(x => x.HeaderTemplate, tpl)
            .Add(x => x.HeaderComponent, typeof(TestHeaderComponent))); // must be ignored

        Assert.NotEmpty(cut.FindAll("button.frag-remove"));
        Assert.Empty(cut.FindAll(".custom-header"));           // component override not used
        Assert.Empty(cut.FindAll(".widget-edit-header"));      // default not used

        cut.Find("button.frag-remove").Click();
        Assert.Equal(1, removed);
    }

    [Fact]
    public void ErrorTemplate_Override_Replaces_The_Default_Error_State()
    {
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        var descriptor = new WidgetDescriptor
        {
            Type = "Boom", Name = "Boom", Category = "T",
            ComponentType = typeof(ThrowingWidget)
        };
        RenderFragment<BlazorDashboardKit.Components.WidgetErrorContext> tpl = ctx => b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "class", "my-error");
            b.AddContent(2, ctx.Exception.Message);
            b.OpenElement(3, "button");
            b.AddAttribute(4, "class", "my-retry");
            b.AddAttribute(5, "onclick", ctx.Recover);
            b.AddContent(6, "retry");
            b.CloseElement();
            b.CloseElement();
        };

        var cut = RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Boom" })
            .Add(x => x.Descriptor, descriptor)
            .Add(x => x.EditMode, false)
            .Add(x => x.ErrorTemplate, tpl));

        Assert.NotEmpty(cut.FindAll(".my-error"));
        Assert.Contains("widget boom", cut.Find(".my-error").TextContent);
        Assert.DoesNotContain("Widget error", cut.Markup);   // default replaced

        // Recover callback resets the kit's ErrorBoundary (no throw).
        cut.Find("button.my-retry").Click();
    }

    [Fact]
    public void Remove_Requires_A_Two_Step_Confirm()
    {
        Services.AddSingleton<IWidgetAccessControl, AllowAllWidgetAccessControl>();
        var removed = 0;
        var cut = RenderComponent<BlazorDashboardKit.Components.WidgetContainer>(p => p
            .Add(x => x.Placement, new WidgetPlacement { WidgetType = "Probe" })
            .Add(x => x.Descriptor, Probe(alwaysInteractive: false))
            .Add(x => x.EditMode, true)
            .Add(x => x.OnRemove, () => removed++));

        // First click only arms the confirm — nothing removed.
        cut.Find("button[title='Remove widget']").Click();
        Assert.Equal(0, removed);
        Assert.NotEmpty(cut.FindAll("button[title='Confirm remove']"));

        // Cancel disarms — still nothing removed, back to the plain Remove button.
        cut.Find("button[title='Cancel remove']").Click();
        Assert.Equal(0, removed);
        Assert.Empty(cut.FindAll("button[title='Confirm remove']"));
        Assert.NotEmpty(cut.FindAll("button[title='Remove widget']"));

        // Arm again then confirm — now it removes exactly once.
        cut.Find("button[title='Remove widget']").Click();
        cut.Find("button[title='Confirm remove']").Click();
        Assert.Equal(1, removed);
    }

    [Fact]
    public void AlwaysInteractive_Widget_Is_Live_When_Host_Interactive_And_Not_ReadOnly()
    {
        var cut = Render(Probe(alwaysInteractive: true), editMode: false, readOnly: false, hostInteractive: true);
        Assert.Contains("RW", cut.Find(".ro-probe").TextContent);
    }

    [Fact]
    public void Host_ReadOnly_Forces_Widget_ReadOnly_Even_If_AlwaysInteractive()
    {
        var cut = Render(Probe(alwaysInteractive: true), editMode: false, readOnly: true, hostInteractive: true);
        Assert.Contains("RO", cut.Find(".ro-probe").TextContent);
    }

    [Fact]
    public void Non_Interactive_Host_Forces_Widget_ReadOnly_Even_If_AlwaysInteractive()
    {
        // Static SSR / prerender: nothing is interactive, so an AlwaysInteractive
        // widget must still render read-only (its handlers would never wire up).
        var cut = Render(Probe(alwaysInteractive: true), editMode: true, readOnly: false, hostInteractive: false);
        Assert.Contains("RO", cut.Find(".ro-probe").TextContent);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Widget_Body_Is_Inert_Only_In_Edit_Mode(bool editMode, bool expectInert)
    {
        // While the dashboard is being arranged, the widget's own links and buttons
        // must not be clickable or reachable by Tab.
        var cut = Render(Probe(alwaysInteractive: false), editMode, readOnly: false, hostInteractive: true);
        Assert.Equal(expectInert, cut.Find(".widget-body").HasAttribute("inert"));
        Assert.NotNull(cut.Find(".widget-body .ro-probe")); // the widget renders inside it
    }
}
