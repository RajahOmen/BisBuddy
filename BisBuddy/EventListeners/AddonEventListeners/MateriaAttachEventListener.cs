using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
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
        // text node containing the name of the gearpiece
        internal static readonly uint AddonGearpieceNameNodeId = 3;
        // hover highlight node for gearpieces
        internal static readonly uint AddonGearpieceSelectedHighlightNodeId = 12;

        // node ids for materia side
        // right side of the melding window (materia)
        internal static readonly uint AddonMateriaComponentNodeId = 16;
        // list of materia to meld to the selected gear
        internal static readonly uint AddonMateriaListNodeId = 23;
        // text node containing the name of the materia
        internal static readonly uint AddonMateriaNameNodeId = 3;
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

        private string selectedItemName = string.Empty;
        internal List<MeldPlan> meldPlans { get; private set; } = [];
        private float previousItemScrollbarY = -1.0f;
        private float previousMateriaScrollbarY = -1.0f;

        internal int selectedMeldPlanIndex = 0;

        protected override void registerAddonListeners()
        {
            Plugin.OnSelectedMeldPlanIdxChange += handleSelectedMateriaPlanIdxChange;
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, handlePostUpdate);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, AddonName, handlePostReceiveEvent);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
        }

        protected override void unregisterAddonListeners()
        {
            Plugin.OnSelectedMeldPlanIdxChange -= handleSelectedMateriaPlanIdxChange;
            Services.AddonLifecycle.UnregisterListener(handlePostUpdate);
            Services.AddonLifecycle.UnregisterListener(handlePostReceiveEvent);
            Services.AddonLifecycle.UnregisterListener(handlePreFinalize);
        }

        public override void handleManualUpdate()
        {
            selectedItemName = string.Empty;
            previousItemScrollbarY = -1.0f;
            previousMateriaScrollbarY = -1.0f;
            handleUpdate();
        }

        public unsafe void handlePostReceiveEvent(AddonEvent type, AddonArgs args)
        {
            var receiveEventArgs = (AddonReceiveEventArgs)args;
            updateSelectedItemName((AtkUnitBase*) receiveEventArgs.Addon);
            previousItemScrollbarY = -1.0f;
            previousMateriaScrollbarY = -1.0f;
        }

        public void handleSelectedMateriaPlanIdxChange(int newIdx)
        {
            if (selectedMeldPlanIndex != newIdx)
            {
                selectedMeldPlanIndex = newIdx;
                previousMateriaScrollbarY = -1.0f;
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

        private void updateMateriaMeldPlans()
        {
            var newMeldPlans = Gearset.GetNeededItemMeldPlans(Plugin.ItemData.GetItemIdByName(selectedItemName), Plugin.Gearsets);

            meldPlans = newMeldPlans;

            // ignore if no melds are needed for this item
            if (newMeldPlans.Count <= 1)
            {
                selectedMeldPlanIndex = 0;
                Plugin.UpdateMeldPlanSelectorWindow(newMeldPlans);
            }
            else
            {
                // ensure index within new bounds
                selectedMeldPlanIndex = Math.Min(meldPlans.Count - 1, selectedMeldPlanIndex);

                // multiple copies of this item with different meld plans requested
                // ex: 2x King Rings, one with 2x CRT, one with 2x DET. Don't highlight both, only one
                Services.Log.Debug($"{newMeldPlans.Count} meld plans found for \"{selectedItemName}\"");
                Plugin.UpdateMeldPlanSelectorWindow(newMeldPlans);
            }
        }

        private unsafe bool updateSelectedItemName(AtkUnitBase* addon)
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
                        Services.Log.Warning($"Unexpected MateriaAttach item name type: {itemNameSeString.Type}");
                    }
                    return false;
                }

                // parse to regular string
                var itemNameString = SeString.Parse(itemNameSeString.String).TextValue;

                // item hasn't changed since last update
                if (itemNameString == selectedItemName) return false;

                selectedItemName = itemNameString;
                Services.Log.Debug($"Item \"{selectedItemName}\" selected in MateriaAttach");

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

        private void handlePostUpdate(AddonEvent type, AddonArgs args)
        {
            handleUpdate();
        }

        private unsafe void handleUpdate()
        {
            try
            {
                var nameUpdated = false;

                var materiaAttach = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);

                if (materiaAttach == null || !materiaAttach->IsVisible) return;

                nameUpdated = updateSelectedItemName(materiaAttach);

                var materiaAttachNode = new BaseNode(materiaAttach);

                var itemScrollbar = materiaAttachNode.GetNestedNode<AtkResNode>([
                    AddonGearpieceListNodeId,
                    AddonGearpieceScrollbarNodeId,
                    AddonGearpieceScrollbarButtonNodeId
                    ]);

                var materiaScrollbar = materiaAttachNode.GetNestedNode<AtkResNode>([
                    AddonMateriaListNodeId,
                    AddonMateriaScrollbarNodeId,
                    AddonMateriaScrollbarButtonNodeId
                    ]);

                if (nameUpdated || itemScrollbar == null || itemScrollbar->Y != previousItemScrollbarY)
                {
                    if (itemScrollbar != null) previousItemScrollbarY = itemScrollbar->Y;
                    updateItemHighlights(materiaAttachNode);
                }

                if (nameUpdated || materiaScrollbar == null || materiaScrollbar->Y != previousMateriaScrollbarY)
                {
                    if (materiaScrollbar != null) previousMateriaScrollbarY = materiaScrollbar->Y;
                    updateMateriaHighlights(materiaAttachNode);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Error in HandleMateriaAttachAddonPostReceiveEvent");
            }
        }

        private unsafe void updateItemHighlights(BaseNode addonNode)
        {
            var gearListNode = addonNode.GetComponentNode(AddonGearpieceListNodeId);

            var gearNeedingMelds = Gearset
                .GetUnmeldedGearpieces(Plugin.Gearsets)
                .Select(g => g.ItemName)
                .ToList();

            HighlightItems(gearNeedingMelds, gearListNode, AddonGearpieceNameNodeId);
        }

        private unsafe void updateMateriaHighlights(BaseNode addonNode)
        {
            var materiaListNode = addonNode.GetComponentNode(AddonMateriaListNodeId);

            // ignore if the item isn't known or not needed by any gearsets
            if (
                string.IsNullOrEmpty(selectedItemName)
                || !Gearset.IsItemIncompleteByName(selectedItemName, Plugin.Gearsets)
                )
            {
                // remove highlights from materia
                HighlightItems([], materiaListNode, AddonMateriaNameNodeId);
                return;
            }

            if (meldPlans.Count <= selectedMeldPlanIndex) return;

            // pick selected materia plan to highlight
            var meldPlan = meldPlans[selectedMeldPlanIndex];

            var meldPlanNames = meldPlan.Materia.Select(m => m.ItemName).ToList();

            HighlightItems(meldPlanNames, materiaListNode, AddonMateriaNameNodeId);
        }

        private unsafe void HighlightItems(
            List<string> nameList,
            ComponentNode parentNode,
            uint nameNodeId
            )
        {
            var nodeList = parentNode.GetComponentNodes();

            for (var i = 0; i < nodeList.Count; i++)
            {
                var nodeItem = nodeList[i];
                var nodeItemNode = nodeItem.GetPointer();

                if (!nodeItemNode->IsVisible()) continue; // not visible, ignore this loop

                var itemNameNode = nodeItem.GetNode<AtkTextNode>(nameNodeId);

                // not a node for listing an item, skip
                if (itemNameNode == null) continue;
                // not a text node, must be the wrong component type
                if (itemNameNode->Type != NodeType.Text) continue;

                // get the name of the item from the text node
                var itemName = MemoryHelper.ReadSeStringNullTerminated((nint)itemNameNode->GetText()).TextValue;

                if (itemName == null || itemName == string.Empty) continue;


                var itemNeeded = nameList
                    .Any(
                        n => itemName.EndsWith("...")
                        ? n.StartsWith(itemName.Replace("...", ""))
                        : n == itemName
                    );

                setNodeNeededMark((AtkResNode*)nodeItemNode, itemNeeded, true, true);
            }
        }

        private void handlePreFinalize(AddonEvent type, AddonArgs? args)
        {
            // disable meld selector window
            Plugin.UpdateMeldPlanSelectorWindow([]);

            // materia attach node deconstructing, forget last item name highlighted
            selectedItemName = string.Empty;
            selectedMeldPlanIndex = 0;

            previousItemScrollbarY = -1.0f;
            previousMateriaScrollbarY = -1.0f;
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
    }
}
