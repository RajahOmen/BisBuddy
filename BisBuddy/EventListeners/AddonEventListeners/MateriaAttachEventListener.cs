using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.EventListeners.AddonEventListeners
{

    public class MateriaAttachEventListener(Plugin plugin)
        : AddonEventListener(plugin, plugin.Configuration.HighlightMateriaMeld)
    {
        public override string AddonName => "MateriaAttach";

        // ADDON NODE IDS
        // node ids for gearpiece side
        // list of gearpieces to meld materia to
        public static readonly uint AddonGearpieceListNodeId = 13;
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

        // for Next Materia behavior
        private readonly Configuration configuration = plugin.Configuration;

        private string selectedItemName = string.Empty;
        private readonly HashSet<int> unmeldedItemIndexes = [];
        private readonly HashSet<int> neededMateriaIndexes = [];
        private HashSet<string> unmeldedItemNames = Gearset.GetUnmeldedItemNames(plugin.Gearsets, plugin.Configuration.HighlightPrerequisiteMateria);
        private HashSet<string> neededMateriaNames = [];

        public List<MeldPlan> meldPlans { get; private set; } = [];
        public int selectedMeldPlanIndex = 0;

        protected override float CustomNodeMaxY => 324f;

        protected override void registerAddonListeners()
        {
            Plugin.OnSelectedMeldPlanIdxChange += handleSelectedMateriaPlanIdxChange;
            Plugin.OnGearsetsUpdate += handleManualUpdate;
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
            unmeldedItemNames = Gearset.GetUnmeldedItemNames(Plugin.Gearsets, Plugin.Configuration.HighlightPrerequisiteMateria);
        }

        protected override void unregisterAddonListeners()
        {
            Plugin.OnSelectedMeldPlanIdxChange -= handleSelectedMateriaPlanIdxChange;
            Plugin.OnGearsetsUpdate -= handleManualUpdate;
            Services.AddonLifecycle.UnregisterListener(handlePreDraw);
            Services.AddonLifecycle.UnregisterListener(handlePreFinalize);
        }

        private void handleManualUpdate()
        {
            unmeldedItemNames = Gearset.GetUnmeldedItemNames(Plugin.Gearsets, Plugin.Configuration.HighlightPrerequisiteMateria);
        }

        private void handlePreFinalize(AddonEvent type, AddonArgs? args)
        {
            // disable meld selector window
            Plugin.UpdateMeldPlanSelectorWindow([]);
        }

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            var updateArgs = (AddonDrawArgs)args;
            var addon = (AtkUnitBase*)updateArgs.Addon;
            if (addon == null || !addon->IsVisible || !addon->WindowNode->IsVisible())
            {
                unmarkNodes();
                return;
            }

            updateState(addon);
            handleUpdate(addon);
        }

        public unsafe void handleSelectedMateriaPlanIdxChange(int newIdx)
            => selectedMeldPlanIndex = newIdx;

        private void updateMateriaMeldPlans()
        {
            if (selectedItemName != string.Empty)
                meldPlans = Gearset.GetNeededItemMeldPlans(
                    Plugin.ItemData.GetItemIdByName(selectedItemName),
                    Plugin.Gearsets,
                    Plugin.Configuration.HighlightPrerequisiteMateria
                    );
            else
                meldPlans.Clear();

            Plugin.UpdateMeldPlanSelectorWindow(meldPlans);

            // ensure index within new bounds
            selectedMeldPlanIndex = Math.Min(
                Math.Max(
                    meldPlans.Count - 1,
                    0
                    ),
                selectedMeldPlanIndex
                );

            // bad index
            if (selectedMeldPlanIndex >= meldPlans.Count)
            {
                neededMateriaNames = [];
                return;
            }

            // update list of materia names that are needed
            var materiaNames = meldPlans[selectedMeldPlanIndex]
                .Materia
                .Where(m => !m.IsMelded)
                .Select(m => m.ItemName);

            // limit to next materia to meld if configured
            if (configuration.HighlightNextMateria)
                materiaNames = materiaNames.Take(1);

            neededMateriaNames = materiaNames.ToHashSet();
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
                        Services.Log.Warning($"Unexpected \"{AddonName}\" item name type \"{itemNameSeString.Type}\"");
                    }
                    return false;
                }

                // parse to regular string
                var itemNameString = SeString.Parse((byte*)itemNameSeString.String).TextValue;

                // item hasn't changed since last update
                if (itemNameString == selectedItemName) return false;

                selectedItemName = itemNameString;
                Services.Log.Debug($"Item \"{selectedItemName}\" selected in \"{AddonName}\"");

                // update the materia meld plan (if there is one)
                updateMateriaMeldPlans();
                return true;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to update selected item name.");
                return false;
            }
        }

        private unsafe void updateRequiredIndexes(
            AtkUnitBase* addon,
            HashSet<int> indexesToFill,
            HashSet<string> neededNames,
            int atkValueListStartIndex
            )
        {
            try
            {
                var atkValues = addon->AtkValues;

                // update didn't include up to selected index = dont have data to update
                if (addon->AtkValuesCount < atkValueListStartIndex + 1) return;

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
                            Services.Log.Warning($"Unexpected {AddonName} name type: {itemName.Type}");
                        }
                        // end of list, break out of loop
                        break;
                    }

                    var itemNameStr = SeString.Parse((byte*)itemName.String).TextValue;

                    if (neededNames.Contains(itemNameStr))
                    {
                        indexesToFill.Add(itemIdx - atkValueListStartIndex);
                    }

                    itemIdx++;
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to update required indexes");
            }
        }

        private unsafe void updateUnmeldedItemIndexes(AtkUnitBase* addon)
        {
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

        private unsafe void handleUpdate(AtkUnitBase* addon)
        {
            try
            {
                if (!addon->IsReady) return;

                var addonNode = new BaseNode(addon);

                updateMateriaHighlights(addonNode);
                updateItemHighlights(addonNode);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Error in HandleMateriaAttachAddonPostReceiveEvent");
            }
        }

        private unsafe void updateItemHighlights(BaseNode addonNode)
        {
            var gearListNode = addonNode.GetComponentNode(AddonGearpieceListNodeId).GetPointer();
            HighlightItems(unmeldedItemIndexes, gearListNode, AddonGearpieceSelectedHighlightNodeId);
        }

        private unsafe void updateMateriaHighlights(BaseNode addonNode)
        {
            var materiaListNode = addonNode.GetComponentNode(AddonMateriaListNodeId).GetPointer();
            HighlightItems(neededMateriaIndexes, materiaListNode, AddonMateriaSelectedHighlightNodeId);
        }

        private unsafe void HighlightItems(
            HashSet<int> highlightedIndexList,
            AtkComponentNode* parentNode,
            uint hoverNodeId
            )
        {
            var parentNodeComponent = (AtkComponentList*)parentNode->Component;

            if (parentNodeComponent->ListLength == 0)
                unmarkNodes((AtkResNode*)parentNode);

            for (var i = 0; i < parentNodeComponent->ListLength; i++)
            {
                var itemNodeComponent = parentNodeComponent->ItemRendererList[i].AtkComponentListItemRenderer;
                var itemNeeded = highlightedIndexList.Contains(itemNodeComponent->ListItemIndex);
                setNodeNeededMark((AtkResNode*)itemNodeComponent->OwnerNode, itemNeeded, true, true);
            }
        }

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon)
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

                customNode = UiHelper.CloneNineGridNode(
                    AddonCustomNodeId,
                    hoverNode,
                    Plugin.Configuration.CustomNodeAddColor,
                    Plugin.Configuration.CustomNodeMultiplyColor,
                    Plugin.Configuration.CustomNodeAlpha
                    ) ?? throw new Exception($"Could not clone node \"{hoverNodeId}\"");

                // mark as dirty
                customNode.InternalNode->DrawFlags |= 0x1;

                // attach it to the addon
                Services.NativeController.AttachToAddon(customNode, addon, addon->RootNode, NodePosition.AsLastChild);

                return customNode;
            }
            catch (Exception ex)
            {
                customNode?.Dispose();
                Services.Log.Error(ex, "Failed to initialize custom node");
                return null;
            }
        }

        protected override unsafe void unlinkCustomNode(nint parentNodePtr, NodeBase node)
        {
            var addon = Services.GameGui.GetAddonByName(AddonName);

            if (addon == nint.Zero)
                return;

            if (parentNodePtr == nint.Zero)
                return;

            Services.NativeController.DetachFromAddon(node, (AtkUnitBase*)addon);
        }
    }
}
