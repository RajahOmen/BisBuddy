using Autofac.Features.AttributeFilters;
using BisBuddy.Resources;
using BisBuddy.Ui.Renderers.Tabs;
using BisBuddy.Ui.Renderers.Tabs.Main;
using BisBuddy.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace BisBuddy.Ui.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly TabRenderer<MainWindowTab> configTabRenderer;
    private readonly ConfigTabState tabState = new()
    {
        ExternalWindow = true
    };
    private bool firstDraw = true;

    public ConfigWindow(
        [KeyFilter(MainWindowTab.PluginConfig)] TabRenderer<MainWindowTab> configTabRenderer
        ) : base($"{string.Format(Resource.ConfigWindowTitle, Constants.PluginName)}###bisbuddyconfiguration22")
    {
        Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(526, 482);
        SizeCondition = ImGuiCond.Appearing;
        SizeConstraints = configTabRenderer.TabSizeConstraints;

        this.configTabRenderer = configTabRenderer;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        base.PreDraw();

        if (firstDraw)
        {
            firstDraw = false;
            configTabRenderer.SetTabState(tabState);
        }

        configTabRenderer.PreDraw();
    }

    public override void Draw()
    {
        configTabRenderer.Draw();
    }
}
