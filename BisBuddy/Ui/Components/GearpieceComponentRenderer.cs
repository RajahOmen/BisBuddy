using BisBuddy.Gear;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using BisBuddy.Resources;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Gear.Prerequisites;

namespace BisBuddy.Ui.Components
{
    public class GearpieceComponentRenderer(
        ITypedLogger<GearpieceComponentRenderer> logger,
        IComponentRendererFactory componentRendererFactory,
        IConfigurationService configurationService
        ) : IComponentRenderer<Gearpiece>
    {
        private readonly ITypedLogger<GearpieceComponentRenderer> logger = logger;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IComponentRendererFactory componentRendererFactory = componentRendererFactory;
        private Gearpiece? gearpiece;

        private UiTheme uiTheme =>
            configurationService.UiTheme;

        public void Initialize(Gearpiece renderableComponent) =>
            gearpiece ??= renderableComponent;

        public void Draw()
        {
            if (gearpiece is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var gearpieceCollected = gearpiece.IsCollected;
            var gearpieceManuallyCollected = gearpiece.IsManuallyCollected;
            var gearpieceNeedsMelds = gearpiece.ItemMateria.Any(m => !m.IsMelded);

            // TODO: Change this?
            var checkmarkColor = new Vector4(1, 1, 1, 1);

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4.0f, 4.0f)))
            {
                using (ImRaii.PushColor(ImGuiCol.CheckMark, checkmarkColor))
                {
                    if (ImGui.Checkbox($"##gearpiece_collected", ref gearpieceCollected))
                    {
                        gearpiece.SetCollected(gearpieceCollected, true);
                        logger.Debug($"Set gearpiece \"{gearpiece.ItemName}\" to {(gearpieceCollected ? "collected" : "not collected")}");
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var tooltip =
                        gearpieceCollected
                        ? gearpieceManuallyCollected
                        ? Resource.ManuallyCollectedTooltip
                        : Resource.AutomaticallyCollectedTooltip
                        : Resource.UncollectedTooltip;
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                //if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                //    searchItemById(gearpiece.ItemId);
                //if (ImGui.IsItemHovered())
                //    ImGui.SetTooltip(string.Format(Resource.SearchInventoryForItemTooltip, gearpiece.ItemName));

                ImGui.SameLine();
            }

            // what label/color to apply to gearpice text
            // by default, fully not collected
            var gearpieceCollectedLabel = "*";
            ////var textColor = UnobtainedColor;
            //if (gearpieceCollected && !gearpieceNeedsMelds)
            //{
            //    gearpieceCollectedLabel = "";
            //    textColor = ObtainedColor;
            //}
            //else if (!gearpieceCollected && !gearpiece.IsObtainable)
            //{
            //    gearpieceCollectedLabel = "*";
            //    textColor = UnobtainedColor;
            //}
            //else
            //{
            //    gearpieceCollectedLabel = "**";
            //    textColor = AlmostObtained;
            //}
            var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(gearpiece.CollectionStatus);

            var hasSubItems = gearpiece.ItemMateria.Any() || gearpiece.PrerequisiteTree != null;

            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
            {
                if (
                ImGui.CollapsingHeader($"{gearpiece.ItemName}{gearpieceCollectedLabel}###gearpiece_collapsing_header")
                && hasSubItems
                )
                {
                    ImGui.Spacing();

                    // don't inherit text color for children
                    using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)))
                    {
                        drawMateriaGroup();
                        drawPrerequisites();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }
            }
        }

        private void drawMateriaGroup()
        {
            if (gearpiece is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetCursorPosX();
            var childHeight = ImGui.GetTextLineHeightWithSpacing() + (ImGui.GetStyle().FramePadding.Y * 2.0f);
            var childHeightPadding = 6.5f;
            var materiaChildHeight = childHeight + (2 * childHeightPadding);
            using (
                ImRaii.Child(
                    "gearpiece_materia_child",
                    new Vector2(windowWidth, childHeight + childHeightPadding * 2),
                    true,
                    ImGuiWindowFlags.AlwaysUseWindowPadding
                    )
                )
            {
                componentRendererFactory
                    .GetComponentRenderer(gearpiece.ItemMateria)
                    .Draw();
                ImGui.Spacing();
            }
        }

        private void drawPrerequisites()
        {
            // TODO: GET CORRECT SIZE
            if (gearpiece?.PrerequisiteTree is IPrerequisiteNode node)
                using (ImRaii.Child("gearpiece_prerequisite_child", new(0,0), border: true))
                {
                    componentRendererFactory
                        .GetComponentRenderer(node)
                        .Draw();
                    ImGui.Spacing();
                }
        }
    }
}
