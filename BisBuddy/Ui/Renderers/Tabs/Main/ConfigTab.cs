using Autofac.Features.Indexed;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Ui.Renderers.Components;
using BisBuddy.Ui.Renderers.Tabs.Config;
using BisBuddy.Ui.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Main;

public class ConfigTab(
    IWindowService windowService,
    IIndex<ConfigWindowTab, TabRenderer<ConfigWindowTab>> tabRendererIndex,
    IAttributeService attributeService
    ) : TabRenderer<MainWindowTab>
{
    private static readonly WindowSizeConstraints? SizeConstraints = new()
    {
        MinimumSize = new(350, 100),
    };

    private List<ConfigWindowTab> configTabsToDraw = Enum
        .GetValues<ConfigWindowTab>()
        .ToList();

    private readonly IIndex<ConfigWindowTab, TabRenderer<ConfigWindowTab>> tabRendererIndex = tabRendererIndex;
    private readonly IAttributeService attributeService = attributeService;

    private bool firstDraw = true;
    private bool showPopoutToggle = true;
    private ConfigWindowTab selectedConfigTab = ConfigWindowTab.General;

    public WindowSizeConstraints? TabSizeConstraints => SizeConstraints;

    public bool ShouldDraw => true;

    public void SetTabState(TabState state)
    {
        if (state is not ConfigTabState configTabState)
            throw new ArgumentException($"State must be type {nameof(ConfigTabState)}");

        showPopoutToggle = !configTabState.ExternalWindow;
    }

    public void PreDraw()
    {
        if (!firstDraw)
            return;

        firstDraw = false;

        configTabsToDraw = configTabsToDraw
            .Where(tab => tabRendererIndex.TryGetValue(tab, out _))
            .ToList();

        selectedConfigTab = configTabsToDraw.FirstOrDefault();
    }

    public void Draw()
    {
        var tableSize = ImGui.GetContentRegionAvail();
        var flags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
        using var table = ImRaii.Table("config_menu_table", 2, flags, tableSize);
        ImGui.PopStyleVar();

        if (!table)
            return;

        ImGui.TableSetupColumn("###config_section_navigation", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("###config_section_details", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var lineHeight = ImGui.GetTextLineHeightWithSpacing() * 1.3f;
        var selectableSize = new Vector2(0, lineHeight);
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
        var rightOffset = 5f * ImGuiHelpers.GlobalScale;
        var navSize = new Vector2(145, 0) * ImGuiHelpers.GlobalScale;
        navSize.X += 10;

        UiComponents.PushTableClipRect();
        try
        {
            using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f)))
            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(2, 5)))
            using (ImRaii.Child("submenu_options_nav", navSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                foreach (var tab in configTabsToDraw)
                {
                    var tabTitle = attributeService.GetEnumAttribute<DisplayAttribute>(tab)!.GetName()!;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + rightOffset);
                    if (ImGui.Selectable(tabTitle, selectedConfigTab == tab, size: selectableSize, flags: ImGuiSelectableFlags.SpanAllColumns))
                        selectedConfigTab = tab;
                }
            }
        }
        finally
        {
            ImGui.PopClipRect();
        }

        ImGui.TableNextColumn();

        var tabPos = ImGui.GetCursorPos();
        var tabContentMax = tabPos + ImGui.GetContentRegionAvail();
        var tabContentsSize = ImGui.GetContentRegionAvail();
        var botRight = ImGui.GetCursorScreenPos() + tabContentsSize;

        UiComponents.PushTableClipRect();
        try
        {
            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8)))
            using (ImRaii.Child("##submenus", tabContentsSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                if (!tabRendererIndex.TryGetValue(selectedConfigTab, out var tabRenderer))
                    throw new ArgumentException($"unknown config menu type: {selectedConfigTab}");

                tabRenderer.Draw();

                if (!showPopoutToggle || windowService.IsWindowOpen(WindowType.Config))
                    return;
            }

            var expandButtonHovered = false;
            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(6)))
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var openConfigWindowButton = FontAwesomeIcon.ArrowUpRightFromSquare.ToIconString();
                var buttonSize = ImGui.CalcTextSize(openConfigWindowButton) + (ImGui.GetStyle().FramePadding * 2 * ImGuiHelpers.GlobalScale);

                ImGui.SetCursorScreenPos(botRight - buttonSize * 1.5f);
                using (ImRaii.Child("open_external_config_window", Vector2.Zero, false))
                {
                    if (ImGui.Button(openConfigWindowButton))
                        windowService.ToggleWindow(WindowType.Config);
                    if (ImGui.IsItemHovered())
                        expandButtonHovered = true;
                }
            }

            if (expandButtonHovered)
                ImGui.SetTooltip(Resource.OpenExternalConfigWindowTooltip);
        }
        finally
        {
            ImGui.PopClipRect();
        }
    }
}
