using BisBuddy.Resources;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Windows.ConfigWindow;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;
    private ConfigMenuGroup selectedConfigMenu = ConfigMenuGroup.General;
    private float? subMenuMaxLength;

    public ConfigWindow(Plugin plugin) : base($"{string.Format(Resource.ConfigWindowTitle, Plugin.PluginName)}###bisbuddyconfiguration")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new()
        {
            MinimumSize = new(300, 0),
            MaximumSize = new(1000, 1000)
        };
        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public void drawGeneralMenu()
    {
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
        {
            ImGui.Text(Resource.GeneralConfigurationHighlightAppearanceCategory);
            ImGui.Spacing();

            // COLOR PICKER
            var existingColor = configuration.DefaultHighlightColor.BaseColor;
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

                            ImGuiColorEditFlags.NoPicker
                            | ImGuiColorEditFlags.AlphaBar
                            | ImGuiColorEditFlags.NoSidePreview
                            | ImGuiColorEditFlags.DisplayRGB
                            | ImGuiColorEditFlags.NoBorder
                        ))
                    {
                        if (existingColor != configuration.DefaultHighlightColor.BaseColor)
                        {
                            configuration.DefaultHighlightColor.UpdateColor(existingColor);
                            plugin.SaveGearsetsWithUpdate();
                        }
                    }
                }
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(Resource.HighlightColorButtonLabel);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightColorHelp);

        // BRIGHT CUSTOM NODE HIGHLIGHTING
        var brightListItemHighlighting = configuration.BrightListItemHighlighting;
        if (ImGui.Checkbox(Resource.BrightListItemHighlightingCheckbox, ref brightListItemHighlighting))
        {
            configuration.BrightListItemHighlighting = brightListItemHighlighting;
            plugin.SaveGearsetsWithUpdate();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.BrightListItemHighlightingHelp);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text(Resource.GeneralConfigurationMiscellaneousCategory);
        ImGui.Spacing();

        //UNCOLLECTED MATERIA HIGHLIGHTING
        var highlightUncollectedItemMateria = configuration.HighlightUncollectedItemMateria;
        if (ImGui.Checkbox(Resource.HighlightUncollectedItemMateriaCheckbox, ref highlightUncollectedItemMateria))
        {
            configuration.HighlightUncollectedItemMateria = highlightUncollectedItemMateria;
            plugin.SaveGearsetsWithUpdate(false);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightUncollectedItemMateriaHelp);

        // ASSIGNMENT GROUPING
        var strictMateriaMatching = configuration.StrictMateriaMatching;
        if (ImGui.Checkbox(Resource.StrictMateriaMatchingCheckbox, ref strictMateriaMatching))
        {
            configuration.StrictMateriaMatching = strictMateriaMatching;
            plugin.SaveConfiguration(true);

            // if auto scanning enabled, rerun assignments with new configuration
            if (configuration.AutoScanInventory)
            {
                plugin.ScheduleUpdateFromInventory(plugin.Gearsets);
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.StrictMateriaMatchingHelp);
    }

    public void drawHighlightingMenu()
    {
        // NEED GREED
        var highlightNeedGreed = configuration.HighlightNeedGreed;
        if (ImGui.Checkbox(Resource.HighlightNeedGreedCheckbox, ref highlightNeedGreed))
        {
            configuration.HighlightNeedGreed = highlightNeedGreed;
            plugin.SaveConfiguration(false);
            plugin.NeedGreedEventListener.SetListeningStatus(highlightNeedGreed);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightNeedGreedHelp);

        // SHOPS
        var highlightShops = configuration.HighlightShops;
        if (ImGui.Checkbox(Resource.HighlightShopExchangesCheckbox, ref highlightShops))
        {
            configuration.HighlightShops = highlightShops;
            plugin.SaveConfiguration(false);
            plugin.ShopExchangeItemEventListener.SetListeningStatus(highlightShops);
            plugin.ShopExchangeCurrencyEventListener.SetListeningStatus(highlightShops);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightShopExchangesHelp);

        // MATERIA MELDING
        // toggle highlighting
        var highlightMateriaMeld = configuration.HighlightMateriaMeld;
        if (ImGui.Checkbox(Resource.HighlightMateriaMeldingCheckbox, ref highlightMateriaMeld))
        {
            configuration.HighlightMateriaMeld = highlightMateriaMeld;
            plugin.SaveConfiguration(false);
            plugin.MateriaAttachEventListener.SetListeningStatus(highlightMateriaMeld);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightMateriaMeldingHelp);

        // next materia vs all materia
        using (ImRaii.Disabled(!highlightMateriaMeld))
        {
            // draw a L shape for parent-child relationship
            var drawList = ImGui.GetWindowDrawList();
            var curLoc = ImGui.GetCursorScreenPos();
            var col = ImGui.GetColorU32(new Vector4(1, 1, 1, 1));
            var halfButtonHeight = ImGui.CalcTextSize("HI").Y / 2 + ImGui.GetStyle().FramePadding.Y;
            drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, halfButtonHeight * 3 + 5), col, 2);
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

            using (ImRaii.PushIndent(25.0f, scaled: false))
            {
                var highlightNextMateria = configuration.HighlightNextMateria;
                if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
                {
                    configuration.HighlightNextMateria = highlightNextMateria;
                    plugin.SaveGearsetsWithUpdate(false);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.HighlightNextMateriaHelp);
            }

            drawList = ImGui.GetWindowDrawList();
            curLoc = ImGui.GetCursorScreenPos();
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

            using (ImRaii.PushIndent(25.0f, scaled: false))
            {
                var highlightPrerequisiteMateria = configuration.HighlightPrerequisiteMateria;
                if (ImGui.Checkbox(Resource.HighlightPrerequisiteMateriaCheckbox, ref highlightPrerequisiteMateria))
                {
                    configuration.HighlightPrerequisiteMateria = highlightPrerequisiteMateria;
                    plugin.SaveGearsetsWithUpdate(false);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.HighlightPrerequisiteMateriaHelp);
            }
        }

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
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightInventoriesHelp);

        // MARKETBOARD
        var highlightMarketboard = configuration.HighlightMarketboard;
        if (ImGui.Checkbox(Resource.HighlightMarketboardCheckbox, ref highlightMarketboard))
        {
            configuration.HighlightMarketboard = highlightMarketboard;
            plugin.SaveConfiguration(false);
            plugin.ItemSearchEventListener.SetListeningStatus(highlightMarketboard);
            plugin.ItemSearchResultEventListener.SetListeningStatus(highlightMarketboard);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightMarketboardHelp);

        // ITEM TOOLTIPS
        var annotateTooltips = configuration.AnnotateTooltips;
        if (ImGui.Checkbox(Resource.HighlightItemTooltipsCheckbox, ref annotateTooltips))
        {
            configuration.AnnotateTooltips = annotateTooltips;
            plugin.SaveConfiguration(false);
            plugin.ItemDetailEventListener.SetListeningStatus(annotateTooltips);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightItemTooltipsHelp);
    }

    public void drawInventoryMenu()
    {
        // ITEM COLLECTION
        var enableAutoComplete = configuration.AutoCompleteItems;
        if (ImGui.Checkbox(Resource.UpdateOnItemChangeCheckbox, ref enableAutoComplete))
        {
            configuration.AutoCompleteItems = enableAutoComplete;
            plugin.SaveConfiguration(true);
            plugin.ItemUpdateEventListener.SetListeningStatus(enableAutoComplete);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.UpdateOnItemChangeHelp);

        // INVENTORY SCAN ON LOGIN/LOAD
        var enableAutoScan = configuration.AutoScanInventory;
        if (ImGui.Checkbox(Resource.UpdateOnLoginLoadCheckbox, ref enableAutoScan))
        {
            configuration.AutoScanInventory = enableAutoScan;
            plugin.SaveConfiguration(true);
            plugin.LoginLoadEventListener.SetListeningStatus(enableAutoScan);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Format(Resource.UpdateOnLoginLoadHelp, Plugin.PluginName));

        // INVENTORY SCAN ON PLUGIN UPDATES
        var enablePluginUpdateScan = configuration.PluginUpdateInventoryScan;
        if (ImGui.Checkbox(Resource.UpdateOnPluginChangesCheckbox, ref enablePluginUpdateScan))
        {
            configuration.PluginUpdateInventoryScan = enablePluginUpdateScan;
            plugin.SaveConfiguration(true);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Format(Resource.UpdateOnPluginChangesHelp, Plugin.PluginName));
    }
    public override void Draw()
    {
        if (subMenuMaxLength is null)
        {
            List<string> subMenuNameLengths = [
            Resource.ConfigGeneralSectionHeader,
            Resource.ConfigHighlightingSectionHeader,
            Resource.ConfigInventorySectionHeader
            ];
            subMenuMaxLength = ImGui.CalcTextSize(subMenuNameLengths.MaxBy(name => name.Length)).X
                + ImGui.GetStyle().FramePadding.X * 5;
        }

        using (ImRaii.Child("subconfig_menu_selection", new(subMenuMaxLength.Value, 230), true))
        {
            if (ImGui.Selectable(Resource.ConfigGeneralSectionHeader, selectedConfigMenu == ConfigMenuGroup.General))
                selectedConfigMenu = ConfigMenuGroup.General;

            ImGui.Spacing();
            if (ImGui.Selectable(Resource.ConfigHighlightingSectionHeader, selectedConfigMenu == ConfigMenuGroup.Highlighting))
                selectedConfigMenu = ConfigMenuGroup.Highlighting;

            ImGui.Spacing();
            if (ImGui.Selectable(Resource.ConfigInventorySectionHeader, selectedConfigMenu == ConfigMenuGroup.Inventory))
                selectedConfigMenu = ConfigMenuGroup.Inventory;
        }

        ImGui.SameLine();

        using (ImRaii.Child("subconfig_settings_panel", new(250, 230), true))
        {
            switch (selectedConfigMenu)
            {
                case ConfigMenuGroup.General:
                    drawGeneralMenu();
                    break;
                case ConfigMenuGroup.Highlighting:
                    drawHighlightingMenu();
                    break;
                case ConfigMenuGroup.Inventory:
                    drawInventoryMenu();
                    break;
                default:
                    throw new ArgumentException($"unknown config menu type: {selectedConfigMenu}");
            }
        }
    }
}
