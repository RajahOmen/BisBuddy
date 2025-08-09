using Autofac.Util;
using BisBuddy.Extensions;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Ui.Config;
using BisBuddy.Util;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Main.Tabs;

public class ConfigTab : TabRenderer, IDisposable
{
    private static readonly WindowSizeConstraints? SizeConstraints = new()
    {
        MinimumSize = new(405, 200),
        MaximumSize = new(1000, 1000),
    };

    private readonly IConfigurationService configurationService;
    private readonly ILanguageService languageService;
    private readonly IAttributeService attributeService;

    private string? longestSubMenuName = null;
    private ConfigMenuGroup selectedConfigMenu = ConfigMenuGroup.General;

    public WindowSizeConstraints? TabSizeConstraints => SizeConstraints;

    public ConfigTab(
        IConfigurationService configurationService,
        ILanguageService languageService,
        IAttributeService attributeService
        )
    {
        this.configurationService = configurationService;
        this.languageService = languageService;
        this.attributeService = attributeService;
        languageService.OnLanguageChange += SetMaxSubMenuCharLength;
    }

    public void Dispose()
    {
        languageService.OnLanguageChange -= SetMaxSubMenuCharLength;
    }

    // TODO: This might break if language is changed dynamically: might need a fix!
    private void SetMaxSubMenuCharLength(string langCode = "") =>
        longestSubMenuName = Enum.GetValues<ConfigMenuGroup>()
            .Select(
                val => attributeService.GetEnumAttribute<DisplayAttribute>(val)?.GetName() ?? ""
            ).MaxBy(
                name => ImGui.CalcTextSize(name).X
            ) ?? "";

    private float CalcSubMenuWidth()
    {
        if (longestSubMenuName == null)
            SetMaxSubMenuCharLength();

        return ImGui.CalcTextSize(longestSubMenuName).X
            + (ImGui.GetStyle().FramePadding.X * 5 * ImGuiHelpers.GlobalScale);
    }

    public void SetTabState(TabState state)
    {
        throw new NotImplementedException();
    }

    public void PreDraw() { }

    public void Draw()
        => Draw(subMenuHeight: 0, panelWidth: 0, panelHeight: 0);

    public void Draw(float subMenuHeight, float panelWidth, float panelHeight)
    {
        var subMenuWidth = CalcSubMenuWidth();

        using (ImRaii.Child("subconfig_menu_selection", new(subMenuWidth, subMenuHeight * ImGuiHelpers.GlobalScale), true))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Constants.SelectableListSpacing * ImGuiHelpers.GlobalScale))
        {
            foreach (var subMenu in Enum.GetValues<ConfigMenuGroup>())
            {
                var subMenuTitle = attributeService.GetEnumAttribute<DisplayAttribute>(subMenu)!.GetName()!;
                if (ImGui.Selectable(subMenuTitle, selectedConfigMenu == subMenu))
                    selectedConfigMenu = subMenu;
            }
        }

        ImGui.SameLine();

        var panelSize = new Vector2(panelWidth * ImGuiHelpers.GlobalScale, panelHeight * ImGuiHelpers.GlobalScale);
        using (ImRaii.Child("subconfig_settings_panel", panelSize, true))
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
                case ConfigMenuGroup.UiTheme:
                    drawUiThemeConfig();
                    break;
                default:
                    throw new ArgumentException($"unknown config menu type: {selectedConfigMenu}");
            }
        }
    }

    private void drawGeneralMenu()
    {
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
        {
            ImGui.Text(Resource.GeneralConfigurationHighlightAppearanceCategory);
            ImGui.Spacing();

            // COLOR PICKER
            var existingColor = configurationService.DefaultHighlightColor.BaseColor;
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
                        | ImGuiColorEditFlags.DisplayRgb
                        | ImGuiColorEditFlags.NoBorder
                        ))
                    {
                        if (existingColor != configurationService.DefaultHighlightColor.BaseColor)
                            configurationService.DefaultHighlightColor.UpdateColor(existingColor);
                    }
                }
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(Resource.HighlightColorButtonLabel);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightColorHelp);

        // BRIGHT CUSTOM NODE HIGHLIGHTING
        var brightListItemHighlighting = configurationService.BrightListItemHighlighting;
        if (ImGui.Checkbox(Resource.BrightListItemHighlightingCheckbox, ref brightListItemHighlighting))
            configurationService.BrightListItemHighlighting = brightListItemHighlighting;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.BrightListItemHighlightingHelp);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text(Resource.GeneralConfigurationMiscellaneousCategory);
        ImGui.Spacing();

        //UNCOLLECTED MATERIA HIGHLIGHTING
        var highlightUncollectedItemMateria = configurationService.HighlightUncollectedItemMateria;
        if (ImGui.Checkbox(Resource.HighlightUncollectedItemMateriaCheckbox, ref highlightUncollectedItemMateria))
            configurationService.HighlightUncollectedItemMateria = highlightUncollectedItemMateria;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightUncollectedItemMateriaHelp);

        // ASSIGNMENT GROUPING
        var strictMateriaMatching = configurationService.StrictMateriaMatching;
        if (ImGui.Checkbox(Resource.StrictMateriaMatchingCheckbox, ref strictMateriaMatching))
            configurationService.StrictMateriaMatching = strictMateriaMatching;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.StrictMateriaMatchingHelp);
    }

    private void drawHighlightingMenu()
    {
        // NEED GREED
        var highlightNeedGreed = configurationService.HighlightNeedGreed;
        if (ImGui.Checkbox(Resource.HighlightNeedGreedCheckbox, ref highlightNeedGreed))
            configurationService.HighlightNeedGreed = highlightNeedGreed;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightNeedGreedHelp);

        // SHOPS
        var highlightShops = configurationService.HighlightShops;
        if (ImGui.Checkbox(Resource.HighlightShopExchangesCheckbox, ref highlightShops))
            configurationService.HighlightShops = highlightShops;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightShopExchangesHelp);

        // MATERIA MELDING
        // toggle highlighting
        var highlightMateriaMeld = configurationService.HighlightMateriaMeld;
        if (ImGui.Checkbox(Resource.HighlightMateriaMeldingCheckbox, ref highlightMateriaMeld))
            configurationService.HighlightMateriaMeld = highlightMateriaMeld;
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
                var highlightNextMateria = configurationService.HighlightNextMateria;
                if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
                    configurationService.HighlightNextMateria = highlightNextMateria;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.HighlightNextMateriaHelp);
            }

            drawList = ImGui.GetWindowDrawList();
            curLoc = ImGui.GetCursorScreenPos();
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

            using (ImRaii.PushIndent(25.0f, scaled: false))
            {
                var highlightPrerequisiteMateria = configurationService.HighlightPrerequisiteMateria;
                if (ImGui.Checkbox(Resource.HighlightPrerequisiteMateriaCheckbox, ref highlightPrerequisiteMateria))
                    configurationService.HighlightPrerequisiteMateria = highlightPrerequisiteMateria;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.HighlightPrerequisiteMateriaHelp);
            }
        }

        // INVENTORIES
        var highlightInventories = configurationService.HighlightInventories;
        if (ImGui.Checkbox(Resource.HighlightInventoriesCheckbox, ref highlightInventories))
            configurationService.HighlightInventories = highlightInventories;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightInventoriesHelp);

        // MARKETBOARD
        var highlightMarketboard = configurationService.HighlightMarketboard;
        if (ImGui.Checkbox(Resource.HighlightMarketboardCheckbox, ref highlightMarketboard))
            configurationService.HighlightMarketboard = highlightMarketboard;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightMarketboardHelp);

        // ITEM TOOLTIPS
        var annotateTooltips = configurationService.AnnotateTooltips;
        if (ImGui.Checkbox(Resource.HighlightItemTooltipsCheckbox, ref annotateTooltips))
            configurationService.AnnotateTooltips = annotateTooltips;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.HighlightItemTooltipsHelp);
    }

    private void drawInventoryMenu()
    {
        // ITEM COLLECTION
        var enableAutoComplete = configurationService.AutoCompleteItems;
        if (ImGui.Checkbox(Resource.UpdateOnItemChangeCheckbox, ref enableAutoComplete))
            configurationService.AutoCompleteItems = enableAutoComplete;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.UpdateOnItemChangeHelp);

        // INVENTORY SCAN ON LOGIN/LOAD
        var enableAutoScan = configurationService.AutoScanInventory;
        if (ImGui.Checkbox(Resource.UpdateOnLoginLoadCheckbox, ref enableAutoScan))
            configurationService.AutoScanInventory = enableAutoScan;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Format(Resource.UpdateOnLoginLoadHelp, Constants.PluginName));

        // INVENTORY SCAN ON PLUGIN UPDATES
        var enablePluginUpdateScan = configurationService.PluginUpdateInventoryScan;
        if (ImGui.Checkbox(Resource.UpdateOnPluginChangesCheckbox, ref enablePluginUpdateScan))
            configurationService.PluginUpdateInventoryScan = enablePluginUpdateScan;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Format(Resource.UpdateOnPluginChangesHelp, Constants.PluginName));
    }

    private void drawUiThemeConfig()
    {
        if (ImGui.Button(Resource.ResetUiThemeButton))
            configurationService.ResetUiTheme();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.ResetUiThemeHelp);
    }
}
