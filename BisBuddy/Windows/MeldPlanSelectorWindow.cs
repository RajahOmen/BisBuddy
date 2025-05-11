using BisBuddy.Gear;
using BisBuddy.Resources;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy.Windows;

public unsafe class MeldPlanSelectorWindow : Window, IDisposable
{
    // how far down from the top of the addon to render the window
    private static readonly int WindowYValueOffset = 76;

    private readonly Configuration configuration;
    private readonly Plugin plugin;
    public List<MeldPlan> MeldPlans = [];
    private AtkUnitBase* addon = null;

    public MeldPlanSelectorWindow(Plugin plugin) : base("Meld Plan###meld plan selector bisbuddy")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

        SizeCondition = ImGuiCond.Appearing;

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (plugin.Configuration.HighlightMateriaMeld)
        {
            addon = null;
            var addonPtr = Services.GameGui.GetAddonByName("MateriaAttach");
            if (addonPtr != nint.Zero)
            {
                addon = (AtkUnitBase*)addonPtr;
            }

            if (Position.HasValue)
                ImGui.SetNextWindowPos(Position.Value, ImGuiCond.Always);

            Position = null;
        }
        base.PreDraw();
    }

    public override void PostDraw()
    {
        if (addon != null && addon->IsVisible)
        {
            // wait until addon is initialised to show
            var rootNode = addon->RootNode;
            if (rootNode == null)
                return;

            short addonWidth = 0;
            short addonHeight = 0;
            addon->GetSize(&addonWidth, &addonHeight, true);
            Position = ImGuiHelpers.MainViewport.Pos + new Vector2(addon->X + addonWidth, addon->Y + WindowYValueOffset);
        }
        base.PostDraw();
    }

    public override void Draw()
    {
        var curIdx = plugin.MateriaAttachEventListener.selectedMeldPlanIndex;
        if (curIdx >= MeldPlans.Count)
            return;

        // don't show if addon isn't currently visible (ie. during a meld action)
        if (addon == null || !addon->IsVisible || !addon->WindowNode->IsVisible())
            return;

        ImGui.Text(Resource.MeldWindowHeader);

        // copy next materia setting from config for convenience
        var highlightNextMateria = configuration.HighlightNextMateria;
        if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
        {
            configuration.HighlightNextMateria = highlightNextMateria;
            plugin.SaveGearsetsWithUpdate(false);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightNextMateriaHelp);

        ImGui.Separator();
        ImGui.Spacing();

        for (var i = 0; i < MeldPlans.Count; i++)
        {
            using var _ = ImRaii.PushId(i);
            var plan = MeldPlans[i];

            var selectablePos = ImGui.GetCursorPos();

            var isSelected = plugin.MateriaAttachEventListener.selectedMeldPlanIndex == i;
            if (ImGui.Selectable($" \n ###materia_plan_selectable", isSelected, ImGuiSelectableFlags.SpanAllColumns))
            {
                plugin.TriggerSelectedMeldPlanChange(i);
                Services.Log.Debug($"Selected meld plan {i}");
            }

            // Overlap plan text with empty selectable
            ImGui.SetCursorPos(selectablePos);

            // reduce vertical space between plan text and materia
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0)))
            {
                // add left spacing
                ImGui.Text($" {plan.PlanText}");
                ImGui.Text($" ");
                ImGui.SameLine();
            }

            foreach (var materia in plan.MateriaInfo)
            {
                var color = materia.IsMelded ? MainWindow.ObtainedColor : MainWindow.UnobtainedColor;
                ImGui.TextColored(color, materia.MateriaText);
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }
    }
}
