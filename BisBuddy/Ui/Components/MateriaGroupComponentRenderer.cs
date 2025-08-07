using BisBuddy.Gear.Melds;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Ui.Components
{
    public class MateriaGroupComponentRenderer(
        ITypedLogger<MateriaGroupComponentRenderer> logger,
        IConfigurationService configurationService,
        IAttributeService attributeService
        ) : IComponentRenderer<MateriaGroup>
    {
        private readonly ITypedLogger<MateriaGroupComponentRenderer> logger = logger;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IAttributeService attributeService = attributeService;
        private MateriaGroup? materiaGroup;

        private UiTheme uiTheme =>
            configurationService.UiTheme;

        public void Initialize(MateriaGroup renderableComponent) =>
            materiaGroup ??= renderableComponent;

        public void Draw()
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

                string needColorblind;
                string meldVerb;
                var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(
                    materia.CollectionStatus
                    );

                if (materia.IsMelded)
                {
                    needColorblind = "";
                    meldVerb = Resource.UnmeldVerb;
                }
                else
                {
                    needColorblind = "*";
                    meldVerb = Resource.MeldVerb;
                }

                var materiaStat = attributeService.GetEnumAttribute<DisplayAttribute>(materia.StatType)!.GetShortName()!;
                var materiaButtonText = $"x{materiaStatusGroup.Count} +{materia.StatQuantity} {materiaStat}{needColorblind}";

                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                {
                    if (ImGui.Button($"{materiaButtonText}##materia_meld_button"))
                    {
                        if (materia.IsMelded)
                        {
                            logger.Verbose($"Unmelding materia \"{materia.ItemId}\"");
                            materiaGroup.UnmeldSingleMateria(materia.ItemId);
                        }
                        else
                        {
                            logger.Verbose($"Melding materia \"{materia.ItemId}\"");
                            materiaGroup.MeldSingleMateria(materia.ItemId);
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Format(Resource.MateriaTooltip, meldVerb, materia.ItemName));
                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                //if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                //    searchItemById(materiaStatusGroup.Type.ItemId);
                ImGui.SameLine();
            }
        }
    }
}
