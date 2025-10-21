using Autofac.Features.Indexed;
using BisBuddy.Gear;
using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Util;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BisBuddy.Ui.Renderers.Tabs;
using BisBuddy.Ui.Renderers.Tabs.Main;

namespace BisBuddy.Ui.Windows;

public class MainWindow : Window, IDisposable
{
    private static readonly WindowSizeConstraints MainSizeConstraints = new()
    {
        MinimumSize = new Vector2(405, 100),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
    };
    private static readonly Vector2 DefaultSize = new(630, 510);

    private readonly IIndex<MainWindowTab, TabRenderer<MainWindowTab>> tabRendererIndex;
    //private readonly List<(MainWindowTab Type, TabRenderer Renderer)> renderersToDraw;
    private readonly IAttributeService attributeService;

    // what tab to currently render
    private MainWindowTab? activeTab = null;
    private MainWindowTab? nextActiveTab = MainWindowTab.UserGearsets;

    private bool isFirstPreDraw = true;
    // order using the value of the enum as a sort key
    private List<MainWindowTab> tabTypes;

    private static readonly string PluginVersion =
        $" v{Assembly.GetExecutingAssembly().GetName().Version}";

    public MainWindow(
        IIndex<MainWindowTab, TabRenderer<MainWindowTab>> tabRendererIndex,
        IAttributeService attributeService
        )
        : base($"{Constants.PluginName}{PluginVersion}##bisbuddymainwindow")
    {
        this.tabRendererIndex = tabRendererIndex;
        this.attributeService = attributeService;
        SizeConstraints = MainSizeConstraints;
        Size = DefaultSize;
        SizeCondition = ImGuiCond.Once;
        tabTypes = Enum.GetValues<MainWindowTab>().OrderBy(t => (int)t).ToList();
    }

    public void Dispose() { }

    public void SetNextActiveTab(MainWindowTab tabTypeToOpen, TabState? tabState)
    {
        if (tabRendererIndex.TryGetValue(tabTypeToOpen, out var tabRenderer))
        {
            if (tabState is not null)
                tabRenderer.SetTabState(tabState);
            nextActiveTab = tabTypeToOpen;
        }
    }

    public override void PreDraw()
    {
        base.PreDraw();

        if (isFirstPreDraw)
        {
            tabTypes = tabTypes
                .Where(t => tabRendererIndex.TryGetValue(t, out var _))
                .ToList();

            isFirstPreDraw = false;
        }

        var nextTab = nextActiveTab ?? activeTab;

        if (nextTab is not MainWindowTab tab)
            return;

        if (!tabRendererIndex.TryGetValue(tab, out var nextTabRenderer))
            return;

        // perform predraw step for tab that is about to be rendered
        nextTabRenderer.PreDraw();
    }

    public override void Draw()
    {
        // Appended ##{num} to update the order if I want to change the tab order in future
        // as dalamud will not update the order unless the name changes
        using var tabBar = ImRaii.TabBar("mainWindowTab##0");
        if (!tabBar)
            return;

        foreach (var tabType in tabTypes)
        {
            if (!tabRendererIndex.TryGetValue(tabType, out var tabRenderer))
                continue;

            if (!tabRenderer.ShouldDraw)
                continue;

            var tabTitle = attributeService
                .GetEnumAttribute<DisplayAttribute>(tabType)!
                .GetName()!;
            var isActiveFlag = nextActiveTab == tabType
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            using var tabItem = ImRaii.TabItem(tabTitle, isActiveFlag);

            if (!tabItem)
                continue;

            if (activeTab != tabType)
                setSizeConstraints(tabRenderer.TabSizeConstraints);

            tabRenderer.Draw();
            activeTab = tabType;
        }

        nextActiveTab = null;
    }

    private void setSizeConstraints(WindowSizeConstraints? tabSizeConstraints)
    {
        if (tabSizeConstraints is not WindowSizeConstraints next)
            return;

        var minSize = new Vector2(
            x: Math.Max(next.MinimumSize.X, MainSizeConstraints.MinimumSize.X),
            y: Math.Max(next.MinimumSize.Y, MainSizeConstraints.MinimumSize.Y)
            );

        var maxSize = new Vector2(
            x: Math.Min(next.MaximumSize.X, MainSizeConstraints.MaximumSize.X),
            y: Math.Min(next.MaximumSize.Y, MainSizeConstraints.MaximumSize.Y)
            );

        SizeConstraints = new()
        {
            MinimumSize = minSize,
            MaximumSize = maxSize
        };
    }
}
