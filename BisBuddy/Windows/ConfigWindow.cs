using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using BisBuddy.Resources;

namespace BisBuddy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base($"{string.Format(Resource.ConfigWindowTitle, Plugin.PluginName)}###bisbuddyconfiguration")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse;

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text(Resource.ConfigHighlightingSectionHeader);
        ImGui.Spacing();

        // NEED GREED
        var highlightNeedGreed = configuration.HighlightNeedGreed;
        if (ImGui.Checkbox(Resource.HighlightNeedGreedCheckbox, ref highlightNeedGreed))
        {
            configuration.HighlightNeedGreed = highlightNeedGreed;
            configuration.Save();
            plugin.NeedGreedEventListener.SetListeningStatus(highlightNeedGreed);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightNeedGreedHelp);

        // SHOPS
        var highlightShops = configuration.HighlightShops;
        if (ImGui.Checkbox(Resource.HighlightShopExchangesCheckbox, ref highlightShops))
        {
            configuration.HighlightShops = highlightShops;
            configuration.Save();
            plugin.ShopExchangeItemEventListener.SetListeningStatus(highlightShops);
            plugin.ShopExchangeCurrencyEventListener.SetListeningStatus(highlightShops);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightShopExchangesHelp);

        // MATERIA MELDING
        var highlightMateriaMeld = configuration.HighlightMateriaMeld;
        if (ImGui.Checkbox(Resource.HighlightMateriaMeldingCheckbox, ref highlightMateriaMeld))
        {
            configuration.HighlightMateriaMeld = highlightMateriaMeld;
            configuration.Save();
            plugin.MateriaAttachEventListener.SetListeningStatus(highlightMateriaMeld);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightMateriaMeldingHelp);

        // INVENTORIES
        var highlightInventories = configuration.HighlightInventories;
        if (ImGui.Checkbox(Resource.HighlightInventoriesCheckbox, ref highlightInventories))
        {
            configuration.HighlightInventories = highlightInventories;
            configuration.Save();
            plugin.InventoryEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryExpansionEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryBuddyEventListener.SetListeningStatus(highlightInventories);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightInventoriesHelp);

        // MARKETBOARD
        var highlightMarketboard = configuration.HighlightMarketboard;
        if (ImGui.Checkbox(Resource.HighlightMarketboardCheckbox, ref highlightMarketboard))
        {
            configuration.HighlightMarketboard = highlightMarketboard;
            configuration.Save();
            plugin.ItemSearchEventListener.SetListeningStatus(highlightMarketboard);
            plugin.ItemSearchResultEventListener.SetListeningStatus(highlightMarketboard);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightMarketboardHelp);

        // ITEM TOOLTIPS
        var annotateTooltips = configuration.AnnotateTooltips;
        if (ImGui.Checkbox(Resource.HighlightItemTooltipsCheckbox, ref annotateTooltips))
        {
            configuration.AnnotateTooltips = annotateTooltips;
            configuration.Save();
            plugin.ItemDetailEventListener.SetListeningStatus(annotateTooltips);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightItemTooltipsHelp);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text(Resource.ConfigInventorySectionHeader);
        ImGui.Spacing();

        // ITEM COLLECTION
        var enableAutoComplete = configuration.AutoCompleteItems;
        if (ImGui.Checkbox(Resource.UpdateOnItemChangeCheckbox, ref enableAutoComplete))
        {
            configuration.AutoCompleteItems = enableAutoComplete;
            configuration.Save();
            plugin.ItemUpdateEventListener.SetListeningStatus(enableAutoComplete);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.UpdateOnItemChangeHelp);

        // INVENTORY SCAN
        var enableAutoScan = configuration.AutoScanInventory;
        if (ImGui.Checkbox(Resource.UpdateOnLoginLoadCheckbox, ref enableAutoScan))
        {
            configuration.AutoScanInventory = enableAutoScan;
            configuration.Save();
            plugin.LoginLoadEventListener.SetListeningStatus(enableAutoScan);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.UpdateOnLoginLoadHelp);

        ImGui.Spacing();
    }
}
