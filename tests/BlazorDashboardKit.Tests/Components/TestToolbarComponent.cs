using BlazorDashboardKit.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorDashboardKit.Tests.Components;

/// <summary>
/// Component-type override for the dashboard toolbar. Implements the seam
/// contract: a single <c>[Parameter] DashboardToolbarContext Context</c>. Renders
/// a recognizable marker with the active dashboard's name and an Edit button
/// wired to the context's EnterEditMode callback.
/// </summary>
public sealed class TestToolbarComponent : ComponentBase
{
    [Parameter] public DashboardToolbarContext Context { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "custom-toolbar");
        builder.AddContent(2, $"toolbar:{Context.Active.Name}:{(Context.EditMode ? "editing" : "viewing")}");
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "class", "custom-edit");
        builder.AddAttribute(5, "onclick", Context.EnterEditMode);
        builder.AddContent(6, "edit");
        builder.CloseElement();
        builder.CloseElement();
    }
}
