using BisBuddy.Gear;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;
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

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
        {
            // COLOR PICKER
            var existingColor = configuration.HighlightColor;
            if (ImGui.ColorButton($"{Resource.HighlightColorButtonTooltip}###ColorPickerButton", existingColor))
            {
                ImGui.OpenPopup($"###ColorPickerPopup");
            }

            using (var popup = ImRaii.Popup($"###ColorPickerPopup"))
            {
                if (popup)
                {
                    if (ImGui.ColorPicker4(
                        $"###ColorPicker",
                        ref existingColor,
                        (
                            ImGuiColorEditFlags.NoPicker
                            | ImGuiColorEditFlags.AlphaBar
                            | ImGuiColorEditFlags.NoSidePreview
                            | ImGuiColorEditFlags.DisplayRGB
                            | ImGuiColorEditFlags.NoBorder
                        )))
                    {
                        if (existingColor != configuration.HighlightColor)
                        {
                            configuration.HighlightColor = existingColor;
                            plugin.SaveGearsetsWithUpdate();
                        }
                    }
                }
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(Resource.HighlightColorButtonLabel);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightColorHelp);

        // NEED GREED
        var highlightNeedGreed = configuration.HighlightNeedGreed;
        if (ImGui.Checkbox(Resource.HighlightNeedGreedCheckbox, ref highlightNeedGreed))
        {
            configuration.HighlightNeedGreed = highlightNeedGreed;
            plugin.SaveConfiguration(false);
            plugin.NeedGreedEventListener.SetListeningStatus(highlightNeedGreed);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightNeedGreedHelp);

        // SHOPS
        var highlightShops = configuration.HighlightShops;
        if (ImGui.Checkbox(Resource.HighlightShopExchangesCheckbox, ref highlightShops))
        {
            configuration.HighlightShops = highlightShops;
            plugin.SaveConfiguration(false);
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
            plugin.SaveConfiguration(false);
            plugin.MateriaAttachEventListener.SetListeningStatus(highlightMateriaMeld);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightMateriaMeldingHelp);

        // INVENTORIES
        var highlightInventories = configuration.HighlightInventories;
        if (ImGui.Checkbox(Resource.HighlightInventoriesCheckbox, ref highlightInventories))
        {
            configuration.HighlightInventories = highlightInventories;
            plugin.SaveConfiguration(false);
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
            plugin.SaveConfiguration(false);
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
            plugin.SaveConfiguration(false);
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
            plugin.SaveConfiguration(true);
            plugin.ItemUpdateEventListener.SetListeningStatus(enableAutoComplete);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.UpdateOnItemChangeHelp);

        // INVENTORY SCAN ON LOGIN/LOAD
        var enableAutoScan = configuration.AutoScanInventory;
        if (ImGui.Checkbox(Resource.UpdateOnLoginLoadCheckbox, ref enableAutoScan))
        {
            configuration.AutoScanInventory = enableAutoScan;
            plugin.SaveConfiguration(true);
            plugin.LoginLoadEventListener.SetListeningStatus(enableAutoScan);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(string.Format(Resource.UpdateOnLoginLoadHelp, Plugin.PluginName));

        // INVENTORY SCAN ON PLUGIN UPDATES
        var enablePluginUpdateScan = configuration.PluginUpdateInventoryScan;
        if (ImGui.Checkbox(Resource.UpdateOnPluginChangesCheckbox, ref enablePluginUpdateScan))
        {
            configuration.PluginUpdateInventoryScan = enablePluginUpdateScan;
            plugin.SaveConfiguration(true);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(string.Format(Resource.UpdateOnPluginChangesHelp, Plugin.PluginName));

        // ASSIGNMENT GROUPING
        var strictMateriaMatching = configuration.StrictMateriaMatching;
        if (ImGui.Checkbox(Resource.StrictMateriaMatchingCheckbox, ref strictMateriaMatching))
        {
            configuration.StrictMateriaMatching = strictMateriaMatching;
            plugin.SaveConfiguration(true);

            // if auto scanning enabled, rerun assignments with new configuration
            if (configuration.AutoScanInventory)
            {
                plugin.UpdateFromInventory(plugin.Gearsets);
            }
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.StrictMateriaMatchingHelp);
    }
}
