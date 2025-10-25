using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Ui.Renderers;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.NativeWrapper;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using System;
using System.Numerics;

namespace BisBuddy.Ui.Windows;

public unsafe class MeldPlanSelectorWindow : Window, IDisposable
{
    // how far down from the top of the addon to render the window
    private static readonly int WindowYValueOffset = 76;

    private readonly ITypedLogger<MeldPlanSelectorWindow> logger;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IMeldPlanService meldPlanService;
    private readonly IConfigurationService configService;
    private readonly IRendererFactory rendererFactory;

    private AtkUnitBasePtr addonPtr = nint.Zero;

    public MeldPlanSelectorWindow(
        ITypedLogger<MeldPlanSelectorWindow> logger,
        IFramework framework,
        IGameGui gameGui,
        IMeldPlanService meldPlanService,
        IConfigurationService configService,
        IRendererFactory rendererFactory
        )
        : base("Meld Plan###meld plan selector bisbuddy")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

        SizeCondition = ImGuiCond.Appearing;

        this.logger = logger;
        this.framework = framework;
        this.gameGui = gameGui;
        this.meldPlanService = meldPlanService;
        this.configService = configService;
        this.rendererFactory = rendererFactory;
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

        var itemSpacing = ImGui.GetStyle().FramePadding;

        var selectableSize = new Vector2(
            0,
            ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().FramePadding.Y * 2 + itemSpacing.Y * 2
            );

        for (var i = 0; i < meldPlanService.CurrentMeldPlans.Count; i++)
        {
            using var _ = ImRaii.PushId(i);
            var plan = meldPlanService.CurrentMeldPlans[i];

            var selectablePos = ImGui.GetCursorPos();

            var isSelected = curIdx == i;
            if (ImGui.Selectable($"###materia_plan_selectable", isSelected, ImGuiSelectableFlags.SpanAllColumns, selectableSize))
            {
                logger.Debug($"Selected meld plan {i}");
                meldPlanService.CurrentMeldPlanIndex = i;
            }

            // Overlap plan text with empty selectable
            var xPos = selectablePos.X + itemSpacing.X;
            selectablePos.X = xPos;
            ImGui.SetCursorPos(selectablePos);
            ImGui.Text($"[{plan.Gearset.ClassJobAbbreviation}] {plan.Gearset.Name}");

            ImGui.SetCursorPosX(xPos);

            rendererFactory
                .GetRenderer(plan.MateriaGroup, RendererType.Component)
                .Draw();

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
