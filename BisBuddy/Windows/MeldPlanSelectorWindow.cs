using BisBuddy.Gear;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Windows;

public unsafe class MeldPlanSelectorWindow : Window, IDisposable
{
    // maximum length of gearset name for a plan
    public static readonly int MaxMeldPlanNameLength = 30;
    // how far down from the top of the addon to render the window
    private static readonly int WindowYValueOffset = 102;

    private readonly Configuration configuration;
    private readonly Plugin plugin;
    public List<MeldPlan> MeldPlans = [];
    private AtkUnitBase* addon = null;

    public MeldPlanSelectorWindow(Plugin plugin) : base("Meld Plan###meld plan selector bisbuddy")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    private List<string> getPopupNames()
    {
        List<string> newPlanNames = [];
        var colorblindIndicator = "*";

        foreach (var plan in MeldPlans)
        {
            var jobAbbrev = plan.Gearset.JobAbbrv;
            var gearsetName = plan.Gearset.Name.Length > MaxMeldPlanNameLength
                ? plan.Gearset.Name[..(MaxMeldPlanNameLength - 2)] + ".."
                : plan.Gearset.Name;
            var materiaAbbrevs = plan
                .Materia
                .OrderByDescending(m => m.StatQuantity)
                .Select(m => $"+{m.StatQuantity} {m.StatShortName}{(m.IsMelded ? "" : colorblindIndicator)}");
            newPlanNames.Add($"[{jobAbbrev}] {gearsetName}\n[{string.Join(" ", materiaAbbrevs)}]");
        }

        return newPlanNames;
    }

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
        if (curIdx >= MeldPlans.Count) return;

        ImGui.Text("Select Materia Melds");
        ImGui.Separator();
        ImGui.Spacing();

        var planNames = getPopupNames();

        for (var i = 0; i < planNames.Count; i++)
        {
            var isSelected = plugin.MateriaAttachEventListener.selectedMeldPlanIndex == i;
            if (ImGui.Selectable($"{planNames[i]}##{i}", isSelected))
            {
                plugin.TriggerSelectedMeldPlanChange(i);
                Services.Log.Debug($"Selected meld plan {i}");
            }
        }
    }
}
