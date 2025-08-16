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
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;
using Dalamud.Game.NativeWrapper;

namespace BisBuddy.Windows;

public unsafe class MeldPlanSelectorWindow : Window, IDisposable
{
    // how far down from the top of the addon to render the window
    private static readonly int WindowYValueOffset = 76;

    private readonly ITypedLogger<MeldPlanSelectorWindow> logger;
    private readonly IGameGui gameGui;
    private readonly IMeldPlanService meldPlanService;
    private readonly IConfigurationService configService;

    private AtkUnitBasePtr addonPtr = nint.Zero;

    public MeldPlanSelectorWindow(
        ITypedLogger<MeldPlanSelectorWindow> logger,
        IGameGui gameGui,
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
        this.meldPlanService = meldPlanService;
        this.configService = configService;
    }

    public void Dispose() { }

    public override void PreOpenCheck()
    {
        if (!configService.HighlightMateriaMeld)
            return;

        if (meldPlanService.CurrentMeldPlans.Count == 0)
            return;

        addonPtr = gameGui.GetAddonByName("MateriaAttach");

        if (!addonPtr.IsVisible || !addonPtr.IsReady)
            return;

        var windowOffset = new Vector2(addonPtr.ScaledSize.X, WindowYValueOffset);
        Position = ImGuiHelpers.MainViewport.Pos + addonPtr.Position + windowOffset;
        IsOpen = true;

        base.PreOpenCheck();
    }

    public override void PreDraw()
    {
        if (Position is Vector2 nextWindowPos)
            ImGui.SetNextWindowPos(nextWindowPos, ImGuiCond.Always);

        base.PreDraw();
    }

    public override void Draw()
    {
        var curIdx = meldPlanService.CurrentMeldPlanIndex;

        ImGui.Text(Resource.MeldWindowHeader);

        drawHighlightNextMateriaCheckbox();

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

        IsOpen = false;
        Position = null;
    }

    private void drawHighlightNextMateriaCheckbox()
    {
        // copy next materia setting from config for convenience
        var highlightNextMateria = configService.HighlightNextMateria;
        if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
            configService.HighlightNextMateria = highlightNextMateria;
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Resource.HighlightNextMateriaHelp);
    }
}
