using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Utility;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class GearsetContextMenu(
        ITypedLogger<GearsetContextMenu> logger,
        IContextMenuEntryFactory factory,
        IGearsetsService gearsetsService,
        IConfigurationService configurationService,
        IAttributeService attributeService,
        IJsonSerializerService jsonSerializerService
        ) : ContextMenuBase<Gearset, GearsetContextMenu>(logger, factory)
    {
        private readonly IGearsetsService gearsetsService = gearsetsService;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IAttributeService attributeService = attributeService;
        private readonly IJsonSerializerService jsonSerializerService = jsonSerializerService;

        private UiTheme uiTheme => configurationService.UiTheme;

        private Vector4 textColorTheme(CollectionStatusType collectionStatusType) =>
            configurationService.UiTheme.GetCollectionStatusTheme(collectionStatusType).TextColor * TextMult;

        protected override List<ContextMenuEntry> buildMenuEntries(Gearset newComponent)
        {
            if (newComponent is not Gearset gearset)
                return [];

            var gearsetTypeName = string.Empty;
            if (gearset.SourceType is ImportGearsetSourceType sourceType)
                gearsetTypeName = attributeService
                    .GetEnumAttribute<DisplayAttribute>(sourceType)!
                    .GetName()!;

            return [
                factory.Create(
                    entryName: Resource.DisabledGearsetTooltip,
                    icon: FontAwesomeIcon.CheckCircle,
                    onClick: () => gearset.IsActive = true,
                    shouldDraw: () => !gearset.IsActive),
                factory.Create(
                    entryName: Resource.EnabledGearsetTooltip,
                    icon: FontAwesomeIcon.TimesCircle,
                    onClick: () => gearset.IsActive = false,
                    shouldDraw: () => gearset.IsActive),
                factory.Create(
                    entryName: Resource.ContextMenuLockAllCollected,
                    icon: FontAwesomeIcon.Lock,
                    textColor: () => textColorTheme(CollectionStatusType.ObtainedComplete),
                    onClick: () => {
                        foreach (var gearpiece in gearset.Gearpieces)
                            gearpiece.SetIsCollectedLocked(true);
                    },
                    shouldDraw: () => gearset.Gearpieces.Any(g => !g.IsCollected || !g.CollectLock)),
                factory.Create(
                    entryName: Resource.ContextMenuLockAllUncollected,
                    icon: FontAwesomeIcon.Lock,
                    textColor: () => textColorTheme(CollectionStatusType.NotObtainable),
                    onClick: () => {
                        foreach (var gearpiece in gearset.Gearpieces)
                            gearpiece.SetIsCollectedLocked(false);
                    },
                    shouldDraw: () => gearset.Gearpieces.Any(g => g.IsCollected || !g.CollectLock)),
                factory.Create(
                    entryName: Resource.ContextMenuUnlockAll,
                    icon: FontAwesomeIcon.Unlock,
                    onClick: () => {
                        foreach (var gearpiece in gearset.Gearpieces)
                            gearpiece.CollectLock = false;
                    },
                    shouldDraw: () => gearset.Gearpieces.Any(g => g.CollectLock)),
                factory.Create(
                    entryName: Resource.ContextMenuCopyString.Format(gearsetTypeName),
                    icon: FontAwesomeIcon.Copy,
                    onClick: () => ImGui.SetClipboardText(gearset.SourceString),
                    shouldDraw: () => gearset.SourceString is not null && gearset.SourceType is not null),
                factory.Create(
                    entryName: Resource.ContextMenuCopyUrl.Format(gearsetTypeName),
                    icon: FontAwesomeIcon.Copy,
                    onClick: () => ImGui.SetClipboardText(gearset.SourceUrl),
                    shouldDraw: () => gearset.SourceUrl is not null && gearset.SourceType is not null),
                factory.Create(
                    entryName: Resource.ContextMenuCopyJson,
                    icon: FontAwesomeIcon.FileExport,
                    onClick: () => ImGui.SetClipboardText(jsonSerializerService.Serialize(gearset))),
                factory.Create(
                    entryName: Resource.ContextMenuDeleteGearset,
                    icon: FontAwesomeIcon.Trash,
                    onClick: () => gearsetsService.RemoveGearset(gearset),
                    backgroundColor: () => uiTheme.DeleteColor),
                ];
        }
    }
}
