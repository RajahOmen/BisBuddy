using Autofac.Features.Indexed;
using BisBuddy.Gear;
using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Ui.Main.Tabs;
using BisBuddy.Util;
using BisBuddy.Ui.Config;
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
using BisBuddy.Extensions;
using BisBuddy.Ui.Renderers;
using System.Reflection;

namespace BisBuddy.Ui;

public class MainWindow : Window, IDisposable
{
    private static readonly WindowSizeConstraints MainSizeConstraints = new()
    {
        MinimumSize = new Vector2(405, 100),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
    };
    private static readonly Vector2 DefaultSize = new(630, 500);

    private readonly ITypedLogger<MainWindow> logger;
    private readonly IClientState clientState;
    private readonly ConfigWindow configWindow;
    private readonly ImportGearsetWindow importGearsetWindow;
    private readonly IGearsetsService gearsetsService;
    private readonly IInventoryUpdateDisplayService inventoryUpdateService;
    private readonly IConfigurationService configurationService;
    private readonly IIndex<MainWindowTab, TabRenderer> tabRendererIndex;
    private readonly IAttributeService attributeService;
    private readonly IItemFinderService itemFinderService;

    // what tab to currently render
    private MainWindowTab? activeTab = null;
    private MainWindowTab? nextActiveTab = MainWindowTab.UserGearsets;

    // order using the value of the enum as a sort key
    private readonly IEnumerable<MainWindowTab> tabTypes = Enum.GetValues<MainWindowTab>().OrderBy(t => (int) t);

    private static readonly string PluginVersion =
        $" v{Assembly.GetExecutingAssembly().GetName().Version}";

    public MainWindow(
        ITypedLogger<MainWindow> logger,
        IClientState clientState,
        ConfigWindow configWindow,
        ImportGearsetWindow importGearsetWindow,
        IGearsetsService gearsetsService,
        IInventoryUpdateDisplayService inventoryUpdateService,
        IConfigurationService configurationService,
        IIndex<MainWindowTab, TabRenderer> tabRendererIndex,
        IAttributeService attributeService,
        IItemFinderService itemFinderService
        )
        : base($"{Constants.PluginName}{PluginVersion}##bisbuddymainwindow")
    {
        this.logger = logger;
        this.clientState = clientState;
        this.configWindow = configWindow;
        this.importGearsetWindow = importGearsetWindow;
        this.gearsetsService = gearsetsService;
        this.inventoryUpdateService = inventoryUpdateService;
        this.configurationService = configurationService;
        this.tabRendererIndex = tabRendererIndex;
        this.attributeService = attributeService;
        this.itemFinderService = itemFinderService;
        SizeConstraints = MainSizeConstraints;
        Size = DefaultSize;
        SizeCondition = ImGuiCond.Appearing;
    }

    public void Dispose() { }

    public void OpenToTab(MainWindowTab tabTypeToOpen, TabState? tabState = null)
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

        // perform predraw step for tab that is about to be rendered
        var nextTab = nextActiveTab ?? activeTab;

        if (
            nextTab is MainWindowTab tab
            && tabRendererIndex.TryGetValue(tab, out var nextTabRenderer)
            )
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
            var tabTitle = attributeService
                .GetEnumAttribute<DisplayAttribute>(tabType)!
                .GetName()!;
            var isActiveFlag = nextActiveTab == tabType
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            using var tabItem = ImRaii.TabItem(tabTitle, isActiveFlag);

            if (tabItem)
                if (tabRendererIndex.TryGetValue(tabType, out var tabRenderer))
                {
                    setSizeConstraints(tabRenderer.TabSizeConstraints);
                    tabRenderer.Draw();
                    activeTab = tabType;
                }
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
