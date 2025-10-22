using BisBuddy.Gear.Melds;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace BisBuddy.Ui.Renderers.Components
{
    public class MateriaGroupComponentRenderer(
        ITypedLogger<MateriaGroupComponentRenderer> logger,
        IConfigurationService configurationService,
        IRendererFactory rendererFactory
        ) : ComponentRendererBase<MateriaGroup>
    {
        private static readonly Vector4 ButtonFillColorMult = new(0.5f, 0.5f, 0.5f, 0.6f);

        private readonly ITypedLogger<MateriaGroupComponentRenderer> logger = logger;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IRendererFactory rendererFactory = rendererFactory;
        private MateriaGroup? materiaGroup;

        private UiTheme uiTheme =>
            configurationService.UiTheme;

        public override void Initialize(MateriaGroup renderableComponent) =>
            materiaGroup ??= renderableComponent;

        public override void Draw()
        {
            if (materiaGroup is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            if (materiaGroup == null || materiaGroup.Count == 0)
                return;

            for (var i = 0; i < materiaGroup.StatusGroups.Count; i++)
            {
                using var _ = ImRaii.PushId(i);
                var materiaStatusGroup = materiaGroup.StatusGroups[i];
                var materia = materiaStatusGroup.Type;

                var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(
                    materia.CollectionStatus
                    );

                var meldVerb = materia.IsCollected
                    ? Resource.UnmeldVerb
                    : Resource.MeldVerb;

                var fillColor = textColor * ButtonFillColorMult;
                var hoverColor = fillColor * 1.2f;
                var activeColor = fillColor * 1.5f;
                var materiaButtonText = $"x{materiaStatusGroup.Count} {materia.StatStrength}";
                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                using (ImRaii.PushColor(ImGuiCol.Button, fillColor))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, hoverColor))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, activeColor))
                using (ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, 1.0f))
                using (ImRaii.Disabled(disabled: !materia.CollectLock))
                {
                    if (ImGui.Button($"{materiaButtonText}##materia_meld_button"))
                    {
                        if (materia.IsCollected)
                        {
                            logger.Verbose($"Unmelding materia \"{materia.ItemId}\"");
                            materiaGroup.UnmeldSingleMateria(materia.ItemId, respectCollectLock: false);
                        }
                        else
                        {
                            logger.Verbose($"Melding materia \"{materia.ItemId}\"");
                            materiaGroup.MeldSingleMateria(materia.ItemId, respectCollectLock: false);
                        }
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    string tooltip;
                    if (materia.CollectLock)
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        tooltip = string.Format(Resource.MateriaMeldTooltip, meldVerb, materia.ItemName);
                    }
                    else
                    {
                        tooltip = string.Format(Resource.GearpieceLockedDisabledTooltip, materia.ItemName);
                    }
                        
                    UiComponents.SetSolidTooltip(string.Format(tooltip, meldVerb, materia.ItemName));
                }
                rendererFactory.GetRenderer(materia, RendererType.ContextMenu).Draw();

                ImGui.SameLine();
            }
        }
    }
}
