using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    internal unsafe class ItemDetailEventListener(Plugin plugin)
        : AddonEventListenerBase(plugin, plugin.Configuration.AnnotateTooltips)
    {
        public override string AddonName => "ItemDetail";

        // ADDON NODE IDS
        // text node for the name of the name of the item being hovered over
        public static readonly uint AddonItemNameTextNodeId = 33;
        // text node for the name of the character that crafted the item
        public static readonly uint AddonCrafterNameTextNodeId = 4;

        // CUSTOM NODE PARAMETERS
        public static readonly ushort CustomNodeWidth = 298;
        public static readonly ushort CustomNodeHeight = 16;
        // top left corner of the custom node
        public static readonly short CustomNodeX = 66;
        public static readonly short CustomNodeY = 45;
        // color of the custom text (green, same as color for what jobs/player level the item is)
        // note: because of the forming of custom se strings and ui color payloads, this is ignored
        // but it has to be something, or else the text doesnt render.
        public static readonly ByteColor CustomNodeTextColor = new() { R = 140, G = 255, B = 90, A = 255 };
        // UIColor code representing the same as above, but is actually used
        public static readonly ushort CustomNodeNormalTextColorType = 43;
        // UIColor representing the color desired to show the quantity required for each gearset (blue)
        public static readonly ushort CustomNodeCountTextColorType = 529;
        // custom text font type
        public static readonly FontType CustomNodeTextFont = FontType.Axis;
        // custom text font size
        public static readonly byte CustomNodeTextSize = 12;
        // max width for custom text when the item name is one line
        public static readonly int CustomNodeMaxTextWidth = 294;
        // max width for custom text when the item name is two lines
        public static readonly int CustomNodeMaxTextWidthTwoLines = 230;
        // how long a gearset name can be before it is truncated
        public static readonly int CustomNodeMaxGearsetNameCharCount = 12;

        // list of gearset names that need the currently hovered item
        private List<(Gearset gearset, int countNeeded)> neededGearsets = [];

        protected override void registerAddonListeners()
        {
            Services.GameGui.HoveredItemChanged += handleHoveredItemChanged;
        }

        protected override void unregisterAddonListeners()
        {
            Services.GameGui.HoveredItemChanged -= handleHoveredItemChanged;
            neededGearsets = [];
        }
        public override void handleManualUpdate()
        {
            // manually trigger refresh
            handleHoveredItemChanged(null, Services.GameGui.HoveredItem);
        }

        protected override unsafe void unlinkCustomNode(nint nodePtr)
        {
            var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null) return; // addon isn't loaded, nothing to unlink

            UiHelper.UnlinkNode((AtkResNode*)nodePtr, addon);
        }

        public SeString usedInSeString(
            AtkTextNode* node,
            bool twoLines
            )
        {
            var startStr = "[Sets: ";
            var endStr = "]";
            var separatorStr = ", ";
            var truncateStr = "..";
            var outputStr = startStr;
            // ensure doesn't overlap with item name if the item name is two lines
            var maxLength = twoLines ? CustomNodeMaxTextWidthTwoLines : CustomNodeMaxTextWidth;
            ushort textWidth = 0;
            ushort textHeight = 0;
            List<(string name, string countNeeded, string separator)> neededStrings = [(startStr, string.Empty, string.Empty)];

            // add gearset names to output string
            for (var i = 0; i < neededGearsets.Count; i++)
            {
                var fullGearsetName = neededGearsets[i].gearset.Name;
                var gearsetCount = $" ({neededGearsets[i].countNeeded})";

                // truncate name if its too long
                var outputGearsetName =
                    fullGearsetName.Length > CustomNodeMaxGearsetNameCharCount - gearsetCount.Length
                    ? fullGearsetName[..(CustomNodeMaxGearsetNameCharCount - (2 + gearsetCount.Length))] + truncateStr
                    : fullGearsetName;

                var separatorLength = (ushort)endStr.Length;
                var nextSeparator = endStr;
                if (i < neededGearsets.Count - 1)
                { // another item later in list, ", " separator needed
                    separatorLength = (ushort)(separatorStr.Length + 3);
                    nextSeparator = separatorStr;
                }

                var newOutputStr = string.Concat(outputStr, outputGearsetName, gearsetCount, nextSeparator);
                var newOutputSeString = new SeString(new TextPayload(newOutputStr)).Encode();
                fixed (byte* newOutputPtr = newOutputSeString)
                {
                    node->GetTextDrawSize(&textWidth, &textHeight, newOutputPtr);
                }

                if (textWidth + separatorLength <= maxLength)
                { // enough space, include it
                    neededStrings.Add((outputGearsetName, gearsetCount, nextSeparator));
                    outputStr = newOutputStr;
                }
                else
                { // not enough space, truncate and end
                    neededStrings.Add((outputGearsetName + $"+{neededGearsets.Count - i}", string.Empty, endStr));
                    break;
                }
            }

            var outputSeString = new SeString(new UIForegroundPayload(CustomNodeNormalTextColorType));

            foreach (var (gearsetName, gearsetCount, separator) in neededStrings)
            {
                var namePayload = new TextPayload(gearsetName);
                outputSeString.Append(namePayload);
                if (gearsetCount != string.Empty)
                {
                    var countColorPayload = new UIForegroundPayload(CustomNodeCountTextColorType);
                    var countPayload = new TextPayload(gearsetCount);
                    var normalColorPayload = new UIForegroundPayload(CustomNodeNormalTextColorType);
                    outputSeString.Append([countColorPayload, countPayload, normalColorPayload]);
                }
                var sepatatorPayload = new TextPayload(separator);
                outputSeString.Append(sepatatorPayload);
            }

            return outputSeString;
        }

        private void setNodeVisibility(bool setVisible)
        {
            var customTextNode = CustomNodes.Count > 0 ? (AtkTextNode*)CustomNodes[0] : null;
            if (customTextNode == null) return; // doesn't exist, nothing to hide
            if (customTextNode->ParentNode == null) return; // no parent node, unconnected from an addon

            setNodeNeededMark(customTextNode->ParentNode, setVisible, false, true);
        }

        private bool updateNeededGearsetNames(uint itemId)
        {
            // get what gearsets need this item
            var newNeededGearsets = Gearset.GetGearsetsNeedingItemById(itemId, Plugin.Gearsets, includeCollectedPrereqs: true);

            if (
                newNeededGearsets.Count == neededGearsets.Count
                && newNeededGearsets.SequenceEqual(neededGearsets)
                )
            {
                return false; // no change
            }

            neededGearsets = newNeededGearsets;
            return true;
        }

        private unsafe bool isItemNameTwoLines(AtkUnitBase* addon)
        {
            // get if the item name is split over two lines or not
            var itemNameTextNode = addon->GetTextNodeById(AddonItemNameTextNodeId);
            var itemName = MemoryHelper.ReadSeStringNullTerminated((nint)itemNameTextNode->GetText()).TextValue;
            return itemName.Contains("\r\n");
        }

        private unsafe void handleHoveredItemChanged(object? sender, ulong itemId)
        {
            try
            {
                if (itemId == 0) return;

                if (itemId > uint.MaxValue)
                {
                    Services.Log.Warning($"{GetType().Name} HoveredItem itemId too large {itemId} > {uint.MaxValue}");
                    return;
                }

                var namesUpdated = updateNeededGearsetNames((uint)itemId);

                if (!namesUpdated) return; // no change

                // update node visibility
                if (neededGearsets.Count == 0)
                {
                    setNodeVisibility(false);
                    return;
                };
                setNodeVisibility(true);

                var addonPtr = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);

                if (addonPtr == null || !addonPtr->IsVisible) return;

                var itemNameIsTwoLines = isItemNameTwoLines(addonPtr);

                // update the custom node with new information
                updateCustomNode(addonPtr, itemNameIsTwoLines);
            }
            catch (Exception e)
            {
                Services.Log.Error(e, "Failed to handle PostRequestedUpdate event");
            }
        }

        private unsafe void updateCustomNode(AtkUnitBase* addon, bool itemNameTwoLines)
        {
            var customTextNode = CustomNodes.Count > 0 ? (AtkTextNode*)CustomNodes[0] : null;
            // assign custom text to node
            if (customTextNode == null) // doesn't exist, create it
            {
                customTextNode = (AtkTextNode*)createCustomNode((nint)addon);
            }

            // get the formatted text to display in the custom node
            var customSeString = usedInSeString(customTextNode, itemNameTwoLines);

            // update node text
            customTextNode->SetText(customSeString.EncodeWithNullTerminator());
        }

        protected override nint initializeCustomNode(nint parentNodePtr)
        {
            var customTextNode = UiHelper.MakeTextNode(AddonCustomNodeId);
            if (customTextNode == null)
            {
                Services.Log.Error($"{GetType().Name}: Failed to create custom text node");
                return 0;
            }

            try
            {
                // text propreties
                customTextNode->TextColor = CustomNodeTextColor;
                customTextNode->FontSize = CustomNodeTextSize;
                customTextNode->FontType = CustomNodeTextFont;
                customTextNode->AlignmentType = AlignmentType.Right;
                // location properties
                customTextNode->SetHeight(CustomNodeHeight);
                customTextNode->SetWidth(CustomNodeWidth);
                customTextNode->SetPositionShort(CustomNodeX, CustomNodeY);
                // general properties
                customTextNode->NodeFlags |= NodeFlags.AnchorTop | NodeFlags.AnchorLeft;
                customTextNode->DrawFlags |= 0x01; // redraw

                UiHelper.LinkNodeAtEnd((AtkResNode*)customTextNode, (AtkUnitBase*)parentNodePtr);
                return (nint)customTextNode;
            }
            catch (Exception ex)
            {
                if (customTextNode != null) UiHelper.FreeTextNode(customTextNode);
                Services.Log.Error(ex, "Failed to initialize custom node");
                return nint.Zero;
            }
        }
    }
}
