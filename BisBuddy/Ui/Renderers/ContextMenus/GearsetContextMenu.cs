using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class GearsetContextMenu(
        ITypedLogger<GearsetContextMenu> logger,
        IContextMenuEntryFactory factory,
        IGearsetsService gearsetsService,
        IConfigurationService configurationService,
        JsonSerializerOptions jsonSerializerOptions
        ) : ContextMenuBase<Gearset, GearsetContextMenu>(logger, factory)
    {
        private static readonly Vector4 HoveredAlpha = new(1, 1, 1, 0.8f);
        private static readonly Vector4 SelectedAlpha = new(1, 1, 1, 0.4f);

        private readonly IGearsetsService gearsetsService = gearsetsService;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly JsonSerializerOptions jsonSerializerOptions = jsonSerializerOptions;

        private UiTheme uiTheme => configurationService.UiTheme;

        protected override List<ContextMenuEntry> buildMenuEntries(Gearset newComponent)
        {
            if (newComponent is not Gearset gearset)
                return [];

            return [
                factory.Create(
                    entryName: "Enable Gearset",
                    icon: FontAwesomeIcon.CheckCircle,
                    onClick: () => gearset.IsActive = true,
                    shouldDraw: () => !gearset.IsActive),
                factory.Create(
                    entryName: "Disable Gearset",
                    icon: FontAwesomeIcon.TimesCircle,
                    onClick: () => gearset.IsActive = false,
                    shouldDraw: () => gearset.IsActive),
                factory.Create(
                    entryName: "Copy JSON",
                    icon: FontAwesomeIcon.Copy,
                    onClick: () => ImGui.SetClipboardText(JsonSerializer.Serialize(gearset, jsonSerializerOptions))),
                factory.Create(
                    entryName: "Delete Gearset",
                    drawFunc: () =>
                    {
                        using (ImRaii.PushColor(ImGuiCol.Header, uiTheme.DeleteColor * SelectedAlpha))
                        using (ImRaii.PushColor(ImGuiCol.HeaderHovered, uiTheme.DeleteColor * HoveredAlpha))
                        using (ImRaii.PushColor(ImGuiCol.HeaderActive, uiTheme.DeleteColor))
                        {
                            return ContextMenuEntry.DefaultDrawFunc("Delete Gearset", FontAwesomeIcon.Trash, setSelected: true);
                        }
                    },
                    onClick: () => gearsetsService.RemoveGearset(gearset))
                ];
        }
    }
}
