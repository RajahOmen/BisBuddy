using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.EventListeners.AddonEventListeners
{

    internal class MateriaAttachEventListener(Plugin plugin)
        : AddonEventListenerBase(plugin, plugin.Configuration.HighlightMateriaMeld)
    {
        public override string AddonName => "MateriaAttach";

        // ADDON NODE IDS
        // node ids for gearpiece side
        // left side of the melding window (gearpieces)
        internal static readonly uint AddonGearpieceComponentNodeId = 2;
        // list of gearpieces to meld materia to
        internal static readonly uint AddonGearpieceListNodeId = 13;
        // hover highlight node for gearpieces
        internal static readonly uint AddonGearpieceSelectedHighlightNodeId = 12;

        // node ids for materia side
        // right side of the melding window (materia)
        internal static readonly uint AddonMateriaComponentNodeId = 16;
        // list of materia to meld to the selected gear
        internal static readonly uint AddonMateriaListNodeId = 23;
        // hover highlight node for materia
        internal static readonly uint AddonMateriaSelectedHighlightNodeId = 7;
        // scrollbar node for the item list
        internal static readonly uint AddonGearpieceScrollbarNodeId = 5;
        // scrollbar button node for the item list
        internal static readonly uint AddonGearpieceScrollbarButtonNodeId = 2;
        // scrollbar node for the materia list
        internal static readonly uint AddonMateriaScrollbarNodeId = 5;
        // scrollbar button node for the materia list
        internal static readonly uint AddonMateriaScrollbarButtonNodeId = 2;

        // ADDON ATKVALUE INDEXES
        // index of the item selected in the gearpiece list
        internal static readonly int AtkValueItemSelectedIndex = 287;
        // start of the list of gearpiece names in the gearpiece list
        internal static readonly int AtkValueItemNameListStartIndex = 147;
        // start of the list of materia names in the materia list
        internal static readonly int AtkValueMateriaNameListStartIndex = 429;
        // value for index of page selected (Gets overwritten when list element hovered/clicked)
        internal static readonly int AtkValuePageIndexSelectedIndex = 4;

        private string selectedItemName = string.Empty;
        private readonly HashSet<int> unmeldedItemIndexes = [];
        private readonly HashSet<int> neededMateriaIndexes = [];
        private HashSet<string> unmeldedGearpieceNames = [];
        private HashSet<string> neededMateriaNames = [];

        internal List<MeldPlan> meldPlans { get; private set; } = [];
        private float previousItemScrollbarY = -1.0f;
        private float previousMateriaScrollbarY = -1.0f;
        private int previousItemPageIndex = -1;

        internal int selectedMeldPlanIndex = 0;

        protected override void registerAddonListeners()
        {
            Plugin.OnSelectedMeldPlanIdxChange += handleSelectedMateriaPlanIdxChange;
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, handlePostSetup);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, handlePostUpdate);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, handlePostRefresh);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
        }

        protected override void unregisterAddonListeners()
        {
            Plugin.OnSelectedMeldPlanIdxChange -= handleSelectedMateriaPlanIdxChange;
            Services.AddonLifecycle.UnregisterListener(handlePostSetup);
            Services.AddonLifecycle.UnregisterListener(handlePostUpdate);
            Services.AddonLifecycle.UnregisterListener(handlePostRefresh);
            Services.AddonLifecycle.UnregisterListener(handlePreFinalize);
        }

        public override unsafe void handleManualUpdate()
        {
            unmeldedGearpieceNames = Gearset
                .GetUnmeldedGearpieces(Plugin.Gearsets)
                .Select(g => g.ItemName)
                .ToHashSet();

            var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null || !addon->IsVisible) return;

            updateState(addon);
        }

        public unsafe void handlePostSetup(AddonEvent type, AddonArgs args)
        {
            unmeldedGearpieceNames = Gearset
                .GetUnmeldedGearpieces(Plugin.Gearsets)
                .Select(g => g.ItemName)
                .ToHashSet();

            updateState((AtkUnitBase*)args.Addon);
        }

        public unsafe void handlePostRefresh(AddonEvent type, AddonArgs args)
        {
            var addon = (AtkUnitBase*)args.Addon;
            if (addon == null || !addon->IsVisible) return;

            if (!addon->IsReady)
            {
                Services.Log.Error($"\"{AddonName}\" isn't ready at \"{GetType().Name}.handlePostRefresh\"");
                return;
            }

            updateState(addon);
        }

        private void handlePreFinalize(AddonEvent type, AddonArgs? args)
        {
            // disable meld selector window
            Plugin.UpdateMeldPlanSelectorWindow([]);
        }

        private unsafe void handlePostUpdate(AddonEvent type, AddonArgs args)
        {
            var updateArgs = (AddonUpdateArgs)args;
            var addon = (AtkUnitBase*)updateArgs.Addon;
            if (addon == null || !addon->IsVisible) return;

            handleUpdate(addon);
        }

        public unsafe void handleSelectedMateriaPlanIdxChange(int newIdx)
        {
            if (selectedMeldPlanIndex != newIdx)
            {
                selectedMeldPlanIndex = newIdx;
                var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null || !addon->IsVisible) return;

                updateState(addon);
            }
        }

        private void updateMateriaMeldPlans()
        {
            var newMeldPlans = Gearset.GetNeededItemMeldPlans(Plugin.ItemData.GetItemIdByName(selectedItemName), Plugin.Gearsets);

            meldPlans = newMeldPlans;
            Plugin.UpdateMeldPlanSelectorWindow(newMeldPlans);

            // ensure index within new bounds
            selectedMeldPlanIndex = Math.Min(
                Math.Max(
                    meldPlans.Count - 1,
                    0
                    ),
                selectedMeldPlanIndex
                );

            // update list of materia names that are needed
            neededMateriaNames =
                meldPlans.Count > selectedMeldPlanIndex
                ? meldPlans[selectedMeldPlanIndex]
                    .Materia
                    .Where(m => !m.IsMelded)
                    .Select(m => m.ItemName)
                    .ToHashSet()
                : [];
        }

        private unsafe bool updatePageIndex(AtkUnitBase* addon)
        {
            if (addon->AtkValuesCount < AtkValuePageIndexSelectedIndex) return false;

            var newPageIndex = addon->AtkValues[AtkValuePageIndexSelectedIndex].Int;

            if (previousItemPageIndex == newPageIndex) return false;

            previousItemPageIndex = newPageIndex;
            return true;
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
                var itemNameString = SeString.Parse(itemNameSeString.String).TextValue;

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

                    var itemNameStr = SeString.Parse(itemName.String).TextValue;

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
                unmeldedGearpieceNames,
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
            updatePageIndex(addon);
            updateItemSelected(addon);
            updateUnmeldedItemIndexes(addon);
            previousItemScrollbarY = -1.0f;

            // right side state updates
            updateMateriaMeldPlans();
            updateNeededMateriaIndexes(addon);
            previousMateriaScrollbarY = -1.0f;
        }

        private unsafe void handleUpdate(AtkUnitBase* addon)
        {
            try
            {
                if (!addon->IsReady) return;

                var addonNode = new BaseNode(addon);

                var itemScrollbar = addonNode.GetNestedNode<AtkResNode>([
                    AddonGearpieceListNodeId,
                    AddonGearpieceScrollbarNodeId,
                    AddonGearpieceScrollbarButtonNodeId
                    ]);

                var materiaScrollbar = addonNode.GetNestedNode<AtkResNode>([
                    AddonMateriaListNodeId,
                    AddonMateriaScrollbarNodeId,
                    AddonMateriaScrollbarButtonNodeId
                    ]);

                if (itemScrollbar == null || itemScrollbar->Y != previousItemScrollbarY)
                {
                    if (itemScrollbar != null) previousItemScrollbarY = itemScrollbar->Y;
                    updateItemHighlights(addonNode);
                }

                if (materiaScrollbar == null || materiaScrollbar->Y != previousMateriaScrollbarY)
                {
                    if (materiaScrollbar != null) previousMateriaScrollbarY = materiaScrollbar->Y;
                    updateMateriaHighlights(addonNode);
                }
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

            for (var i = 0; i < parentNodeComponent->ListLength; i++)
            {
                var itemNodeComponent = parentNodeComponent->ItemRendererList[i].AtkComponentListItemRenderer;
                var itemNeeded = highlightedIndexList.Contains(itemNodeComponent->ListItemIndex);
                setNodeNeededMark((AtkResNode*)itemNodeComponent->OwnerNode, itemNeeded, true, true);
            }
        }

        protected override unsafe nint initializeCustomNode(nint parentNodePtr)
        {
            AtkNineGridNode* customHighlightNode = null;
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

                customHighlightNode = UiHelper.CloneNineGridNode(AddonCustomNodeId, hoverNode);
                customHighlightNode->SetAlpha(255);
                customHighlightNode->AddRed = -255;
                customHighlightNode->AddBlue = -255;
                customHighlightNode->AddGreen = 255;
                customHighlightNode->DrawFlags |= 0x01; // force a redraw ("dirty flag")
                UiHelper.LinkNodeAfterTargetNode((AtkResNode*)customHighlightNode, parentNode, (AtkResNode*)hoverNode);

                return (nint)customHighlightNode;
            }
            catch (Exception ex)
            {
                if (customHighlightNode != null) UiHelper.FreeNineGridNode(customHighlightNode);
                Services.Log.Error(ex, "Failed to initialize custom node");
                return nint.Zero;
            }
        }

        protected override unsafe void unlinkCustomNode(nint nodePtr)
        {
            var node = (AtkResNode*)nodePtr;
            var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null) return; // addon isn't loaded, nothing to unlink
            if (node->ParentNode == null) return; // node isn't linked, nothing to unlink

            UiHelper.UnlinkNode(node, (AtkComponentNode*)node->ParentNode);
        }
    }
}
