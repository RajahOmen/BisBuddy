using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;

namespace BisBuddy.Windows;

public unsafe class MeldPlanSelectorWindow : Window, IDisposable
{
    // how far down from the top of the addon to render the window
    private static readonly int WindowYValueOffset = 76;

    private readonly ITypedLogger<MeldPlanSelectorWindow> logger;
    private readonly IGameGui gameGui;
    private readonly IGearsetsService gearsetsService;
    private readonly IMeldPlanService meldPlanService;
    private readonly IConfigurationService configService;

    private AtkUnitBase* addon = null;

    public MeldPlanSelectorWindow(
        ITypedLogger<MeldPlanSelectorWindow> logger,
        IGameGui gameGui,
        IGearsetsService gearsetsService,
        IMeldPlanService meldPlanService,
        IConfigurationService configService
        )
        : base("Meld Plan###meld plan selector bisbuddy")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

        SizeCondition = ImGuiCond.Appearing;

        this.logger = logger;
        this.gameGui = gameGui;
        this.gearsetsService = gearsetsService;
        this.meldPlanService = meldPlanService;
        this.configService = configService;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (configService.HighlightMateriaMeld)
        {
            addon = null;
            var addonPtr = gameGui.GetAddonByName("MateriaAttach");
            if (addonPtr != nint.Zero)
                addon = (AtkUnitBase*)addonPtr;

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
        var curIdx = meldPlanService.CurrentMeldPlanIndex;

        // don't show if addon isn't currently visible (ie. during a meld action)
        if (addon == null || !addon->IsVisible || !addon->WindowNode->IsVisible())
            return;

        ImGui.Text(Resource.MeldWindowHeader);

        // copy next materia setting from config for convenience
        var highlightNextMateria = configService.HighlightNextMateria;
        if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
        {
            configService.HighlightNextMateria = highlightNextMateria;
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightNextMateriaHelp);

        ImGui.Separator();
        ImGui.Spacing();

        for (var i = 0; i < meldPlanService.CurrentMeldPlans.Count; i++)
        {
            using var _ = ImRaii.PushId(i);
            var plan = meldPlanService.CurrentMeldPlans[i];

            var selectablePos = ImGui.GetCursorPos();

            var isSelected = curIdx == i;
            if (ImGui.Selectable($" \n ###materia_plan_selectable", isSelected, ImGuiSelectableFlags.SpanAllColumns))
            {
                logger.Debug($"Selected meld plan {i}");
                meldPlanService.CurrentMeldPlanIndex = i;
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
