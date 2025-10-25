using BisBuddy.Gear;
using BisBuddy.Mediators;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.System;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.Services.Addon
{

    public class MateriaAttachService(
        AddonServiceDependencies<MateriaAttachService> deps,
        IMeldPlanService meldPlanService
        ) : AddonService<MateriaAttachService>(deps)
    {
        private readonly IMeldPlanService meldPlanService = meldPlanService;

        // for marking the filter checkbox red when filtering is enabled
        private static readonly HighlightColor RedColor = new(1.0f, -1.0f, -1.0f, 1.0f);

        public override string AddonName => "MateriaAttach";

        // ADDON NODE IDS
        // node ids for gearpiece side
        // list of gearpieces to meld materia to
        public static readonly uint AddonGearpieceListNodeId = 13;
        // component for the checkbox to filter out gearpieces that cannot have more materia attached
        public static readonly uint AddonGearpieceFilterCheckboxNodeId = 7;
        // image node for when the checkbox is filled / selected
        public static readonly uint AddonGearpieceFilterCheckboxFilledImageNodeId = 3;
        // hover highlight node for gearpieces
        public static readonly uint AddonGearpieceSelectedHighlightNodeId = 12;

        // node ids for materia side
        // list of materia to meld to the selected gear
        public static readonly uint AddonMateriaListNodeId = 23;
        // hover highlight node for materia
        public static readonly uint AddonMateriaSelectedHighlightNodeId = 7;

        // ADDON ATKVALUE INDEXES
        // index of the item selected in the gearpiece list
        public static readonly int AtkValueItemSelectedIndex = 287;
        // start of the list of gearpiece names in the gearpiece list
        public static readonly int AtkValueItemNameListStartIndex = 147;
        // start of the list of materia names in the materia list
        public static readonly int AtkValueMateriaNameListStartIndex = 429;
        // value for index of page selected (Gets overwritten when list element hovered/clicked)
        public static readonly int AtkValuePageIndexSelectedIndex = 4;

        private string selectedItemName = string.Empty;
        private readonly Dictionary<int, HighlightColor> unmeldedItemIndexes = [];
        private readonly Dictionary<int, HighlightColor> neededMateriaIndexes = [];
        private Dictionary<string, HighlightColor> unmeldedItemNames = deps.GearsetsService.GetUnmeldedMateriaColors();
        private Dictionary<string, HighlightColor> neededMateriaNames = [];

        protected override float CustomNodeMaxY => 324f;

        protected override void registerAddonListeners()
        {
            gearsetsService.OnGearsetsChange += handleManualUpdate;
            addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
            addonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
            unmeldedItemNames = gearsetsService.GetUnmeldedMateriaColors();
        }

        protected override void unregisterAddonListeners()
        {
            gearsetsService.OnGearsetsChange -= handleManualUpdate;
            addonLifecycle.UnregisterListener(handlePreDraw);
            addonLifecycle.UnregisterListener(handlePreFinalize);
        }

        protected override void updateListeningStatus(bool effectsAssignments)
            => setListeningStatus(configurationService.HighlightMateriaMeld);

        private void handleManualUpdate() =>
            unmeldedItemNames = gearsetsService.GetUnmeldedMateriaColors();

        private void handlePreFinalize(AddonEvent type, AddonArgs? args) =>
            meldPlanService.SetCurrentMeldPlanItemId(null);

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            var updateArgs = (AddonDrawArgs)args;
            var addon = (AtkUnitBase*)updateArgs.Addon.Address;
            if (addon == null || !addon->IsVisible || !addon->WindowNode->IsVisible())
            {
                unmarkNodes();
                return;
            }

            updateState(addon);
            updateHighlights(addon);
        }

        private void updateMateriaMeldPlans()
        {
            uint? selectedItemId = selectedItemName != string.Empty
                ? itemDataService.GetItemIdByName(selectedItemName)
                : null;

            meldPlanService.SetCurrentMeldPlanItemId(selectedItemId);

            var currentMeldPlan = meldPlanService.CurrentMeldPlan;

            // update list of materia names that are needed
            var materiaNames = meldPlanService
                .CurrentMeldPlan?
                .MateriaGroup
                .Where(m => !m.IsCollected)
                .Select(m => m.ItemName) ?? [];

            // limit to next materia to meld if configured
            if (configurationService.HighlightNextMateria)
                materiaNames = materiaNames.Take(1);

            var materiaColor = currentMeldPlan?.Gearset.HighlightColor ?? configurationService.DefaultHighlightColor;
            neededMateriaNames = materiaNames.Select(name => (name, materiaColor)).Distinct().ToDictionary();
        }

        private unsafe bool updateItemSelected(AtkUnitBase* addon)
        {
            try
            {
                var atkValues = addon->AtkValues;

                // update didn't include up to selected index = update didnt change the selected item
                if (addon->AtkValuesCount < AtkValueItemNameListStartIndex + 1) return false;

                // get the index of the item selected in the gear list
                var selectedItemIndex = atkValues[AtkValueItemSelectedIndex].Int;

                // no item selected at all
                if (selectedItemIndex < 0 && selectedItemName != string.Empty)
                {
                    selectedItemName = string.Empty;
                    updateMateriaMeldPlans();
                    return true;
                }

                // get the name of the item from the list of gear list item names
                var itemNameSeString = atkValues[AtkValueItemNameListStartIndex + selectedItemIndex];

                // ignore if the item name is null (page turning can deselect item)
                if (
                    itemNameSeString.Type != ValueType.String
                    && itemNameSeString.Type != ValueType.ManagedString
                    )
                {
                    if (itemNameSeString.Type != ValueType.Undefined)
                    {
                        logger.Warning($"Unexpected \"{AddonName}\" item name type \"{itemNameSeString.Type}\"");
                    }
                    return false;
                }

                // parse to regular string
                var itemNameString = SeString.Parse((byte*)itemNameSeString.String).TextValue;

                // some languages split HQ icon into separate raw text payload, add space between if so
                if (itemNameString.EndsWith(Constants.HqIcon) && itemNameString[^2] != ' ')
                    itemNameString = itemNameString.Insert(itemNameString.Length - 1, " ");

                // item hasn't changed since last update
                if (itemNameString == selectedItemName) return false;

                selectedItemName = itemNameString;
                logger.Debug($"Item \"{selectedItemName}\" selected in \"{AddonName}\"");

                // update the materia meld plan (if there is one)
                updateMateriaMeldPlans();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to update selected item name.");
                return false;
            }
        }

        private unsafe void updateRequiredIndexes(
            AtkUnitBase* addon,
            Dictionary<int, HighlightColor> indexesToFill,
            Dictionary<string, HighlightColor> neededNames,
            int atkValueListStartIndex
            )
        {
            try
            {
                var atkValues = addon->AtkValues;

                // update didn't include up to selected index = dont have data to update
                if (addon->AtkValuesCount < atkValueListStartIndex + 1)
                    return;

                indexesToFill.Clear();

                var itemIdx = atkValueListStartIndex;
                while (itemIdx < addon->AtkValuesCount)
                {
                    var itemName = addon->AtkValues[itemIdx];

                    if (
                        itemName.Type != ValueType.String
                        && itemName.Type != ValueType.ManagedString
                        )
                    {
                        if (itemName.Type != ValueType.Undefined)
                        {
                            logger.Warning($"Unexpected {AddonName} name type: {itemName.Type}");
                        }
                        // end of list, break out of loop
                        break;
                    }

                    var itemNameString = SeString.Parse((byte*)itemName.String).TextValue;

                    // some languages split HQ icon into separate raw text payload, add space between if so
                    if (itemNameString.EndsWith(Constants.HqIcon) && itemNameString[^2] != ' ')
                        itemNameString = itemNameString.Insert(itemNameString.Length - 1, " ");

                    if (neededNames.TryGetValue(itemNameString, out var color))
                        indexesToFill.Add(itemIdx - atkValueListStartIndex, color);

                    itemIdx++;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to update required indexes");
            }
        }

        private unsafe void updateUnmeldedItemIndexes(AtkUnitBase* addon)
        {
            // check if filtering right side items is enabled. If so, do not highlight right items
            // and mark the button in red to indicate that it should be adjusted
            var baseNode = new BaseNode(addon);
            var checkboxComponentNode = baseNode
                .GetComponentNode(AddonGearpieceFilterCheckboxNodeId);
            var checkboxComponentNodePtr = checkboxComponentNode.GetPointer();
            var checkboxFillNode = checkboxComponentNode
                .GetNode<AtkImageNode>(AddonGearpieceFilterCheckboxFilledImageNodeId);

            if (checkboxFillNode != null && !checkboxFillNode->IsVisible())
            {
                setNodeNeededMark((AtkResNode*)checkboxComponentNodePtr, RedColor, true, false);
                unmeldedItemIndexes.Clear();
                return;
            }

            setNodeNeededMark((AtkResNode*)checkboxComponentNodePtr, null, true, false);

            // update with item parameters
            updateRequiredIndexes(
                addon,
                unmeldedItemIndexes,
                unmeldedItemNames,
                AtkValueItemNameListStartIndex
                );
        }

        private unsafe void updateNeededMateriaIndexes(AtkUnitBase* addon)
        {
            // update with materia parameters
            updateRequiredIndexes(
                addon,
                neededMateriaIndexes,
                neededMateriaNames,
                AtkValueMateriaNameListStartIndex
                );
        }

        private unsafe void updateState(AtkUnitBase* addon)
        {
            // left side state updates
            updateItemSelected(addon);
            updateUnmeldedItemIndexes(addon);

            // right side state updates
            updateMateriaMeldPlans();
            updateNeededMateriaIndexes(addon);
        }

        private unsafe void updateHighlights(AtkUnitBase* addon)
        {
            try
            {
                if (!addon->IsReady)
                    return;

                var addonNode = new BaseNode(addon);

                updateMateriaHighlights(addonNode);
                updateItemHighlights(addonNode);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating highlights");
            }
        }

        private unsafe void updateItemHighlights(BaseNode addonNode)
        {
            var gearListNode = addonNode.GetComponentNode(AddonGearpieceListNodeId).GetPointer();
            HighlightItems(unmeldedItemIndexes, gearListNode);
        }

        private unsafe void updateMateriaHighlights(BaseNode addonNode)
        {
            var materiaListNode = addonNode.GetComponentNode(AddonMateriaListNodeId).GetPointer();
            HighlightItems(neededMateriaIndexes, materiaListNode);
        }

        private unsafe void HighlightItems(
            Dictionary<int, HighlightColor> highlightedIndexColors,
            AtkComponentNode* parentNode
            )
        {
            var parentNodeComponent = (AtkComponentList*)parentNode->Component;

            if (highlightedIndexColors.Count == 0 || parentNodeComponent->ListLength == 0)
                unmarkNodes((AtkResNode*)parentNode);

            for (var i = 0; i < parentNodeComponent->ListLength; i++)
            {
                var itemNodeComponent = parentNodeComponent->ItemRendererList[i].AtkComponentListItemRenderer;
                var itemColor = highlightedIndexColors.GetValueOrDefault(itemNodeComponent->ListItemIndex);
                setNodeNeededMark((AtkResNode*)itemNodeComponent->OwnerNode, itemColor, true, true);
            }
        }

        protected override unsafe NodeBase initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            NineGridNode? customNode = null;
            try
            {
                var parentNode = (AtkComponentNode*)parentNodePtr;
                var hoverNodeId = parentNode->ParentNode->NodeId == AddonGearpieceListNodeId
                    ? AddonGearpieceSelectedHighlightNodeId
                    : AddonMateriaSelectedHighlightNodeId;

                var hoverNode = ((AtkComponentNode*)parentNodePtr)
                    ->GetComponent()
                    ->UldManager
                    .SearchNodeById(hoverNodeId)
                    ->GetAsAtkNineGridNode();

                customNode = UiHelper.CloneHighlightNineGridNode(
                    hoverNode,
                    color.CustomNodeColor,
                    color.CustomNodeAlpha(configurationService.BrightListItemHighlighting)
                    ) ?? throw new InvalidOperationException($"Could not clone node \"{hoverNodeId}\"");

                return customNode;
            }
            catch
            {
                customNode?.Dispose();
                throw;
            }
        }
    }
}
