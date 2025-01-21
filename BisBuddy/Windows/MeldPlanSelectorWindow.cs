using BisBuddy.Gear;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy.Windows;

public unsafe class MeldPlanSelectorWindow : Window, IDisposable
{
    public static readonly int MaxMeldPlanNameLength = 30;
    private readonly Configuration configuration;
    private readonly Plugin plugin;
    private List<string> planNames = [];
    private AtkUnitBase* addon = null;

    public MeldPlanSelectorWindow(Plugin plugin) : base("Meld Plan###meld plan selector bisbuddy")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public void UpdatePopupNames(List<MeldPlan> meldPlans)
    {
        List<string> newPlanNames = [];
        Dictionary<Gearset, int> counts = [];
        foreach (var plan in meldPlans)
        {
            if (!counts.TryGetValue(plan.Gearset, out var value))
            {
                counts[plan.Gearset] = 0;
            }
            else
            {
                counts[plan.Gearset] = ++value;
            }
        }

        foreach (var kvp in counts)
        {
            // cut off name if too long
            var gearsetName = kvp.Key.Name.Length > MaxMeldPlanNameLength
                ? (kvp.Key.Name[..(MaxMeldPlanNameLength - 3)] + "...")
                : kvp.Key.Name;
            if (kvp.Value > 1)
            {
                for (var i = 0; i < kvp.Value; i++)
                {
                    newPlanNames.Add($"{i + 1}:{kvp.Key.JobAbbrv}-{gearsetName}");
                }
            }
            else
            {
                newPlanNames.Add($"{gearsetName}");
            }
        }
        planNames = newPlanNames;
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
            Position = ImGuiHelpers.MainViewport.Pos + new Vector2(addon->X + addonWidth, addon->Y + 102);
        }
        base.PostDraw();
    }

    public override void Draw()
    {
        var curIdx = plugin.MateriaAttachEventListener.selectedMeldPlanIndex;
        if (curIdx >= planNames.Count) return;


        ImGui.Text("Select Gearset Melds");
        ImGui.Separator();
        ImGui.Spacing();
        for (var i = 0; i < planNames.Count; i++)
        {
            var isSelected = plugin.MateriaAttachEventListener.selectedMeldPlanIndex == i;
            if (ImGui.Selectable(planNames[i], isSelected))
            {
                plugin.MateriaAttachEventListener.selectedMeldPlanIndex = i;
                Services.Log.Debug($"Selecting meld plan {i}");
            }
        }
    }
}
