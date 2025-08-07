using BisBuddy.Resources;
using BisBuddy.Ui.Main.Tabs;
using BisBuddy.Util;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;

namespace BisBuddy.Ui.Config;

public class ConfigWindow : Window, IDisposable
{
    private readonly ConfigTab configTabRenderer;

    public ConfigWindow(
        ConfigTab configTabRenderer
        ) : base($"{string.Format(Resource.ConfigWindowTitle, Constants.PluginName)}###bisbuddyconfiguration")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new()
        {
            MinimumSize = new(300, 200),
            MaximumSize = new(1000, 1000)
        };

        this.configTabRenderer = configTabRenderer;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // just render a constrained view of the config tab
        configTabRenderer.Draw(
            subMenuHeight: 230,
            panelHeight: 230,
            panelWidth: 250
            );
    }
}
