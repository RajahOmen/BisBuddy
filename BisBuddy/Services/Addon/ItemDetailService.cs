using BisBuddy.Gear;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Services.Addon
{
    public unsafe class ItemDetailService(AddonServiceDependencies deps)
        : AddonService(deps, configBool: deps.ConfigService.Config.AnnotateTooltips)
    {
        public override string AddonName => "ItemDetail";

        // ADDON NODE IDS
        // text node for the name of the name of the item being hovered over
        public static readonly uint AddonItemNameTextNodeId = 33;
        // root node
        public static readonly uint AddonParentNodeId = 1;

        // CUSTOM NODE PARAMETERS
        public static readonly ushort CustomNodeWidth = 298;
        public static readonly ushort CustomNodeHeight = 16;
        // top left corner of the custom node
        public static readonly Vector2 CustomNodePosition = new(66, 45);
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
        public static readonly int CustomNodeMaxTextWidth = 292;
        // max width for custom text when the item name is two lines
        public static readonly int CustomNodeMaxTextWidthTwoLines = 230;
        // how long a gearset name can be before it is truncated
        public static readonly int CustomNodeMaxGearsetNameLength = 12;
        // Additional length to add to gearset name if there are a small number of gearsets that need the item
        public static readonly int CustomNodeGearsetNameLengthShortList = 20;

        // list of gearset names that need the currently hovered item
        private List<(Gearset gearset, int countNeeded)> neededGearsets = [];

        // Doesn't really use a node color, use this as a standin
        private static readonly HighlightColor TextNodeColor = new(0.0f, 0.0f, 0.0f, 1.0f);

        protected override float CustomNodeMaxY => float.MaxValue;

        protected override void registerAddonListeners()
        {
            gearsetsService.OnGearsetsChange += handleManualUpdate;
            gameGui.HoveredItemChanged += handleHoveredItemChanged;
        }

        protected override void unregisterAddonListeners()
        {
            gearsetsService.OnGearsetsChange -= handleManualUpdate;
            gameGui.HoveredItemChanged -= handleHoveredItemChanged;
            neededGearsets = [];
        }
        private void handleManualUpdate()
        {
            // manually trigger refresh
            handleHoveredItemChanged(null, gameGui.HoveredItem);
        }

        private unsafe void handleHoveredItemChanged(object? sender, ulong itemId)
        {
            if (itemId == 0)
                return;
            try
            {
                if (itemId > uint.MaxValue)
                {
                    pluginLog.Warning($"{GetType().Name} HoveredItem itemId too large {itemId} > {uint.MaxValue}");
                    return;
                }

                var namesUpdated = updateNeededGearsetNames((uint)itemId);

                //pluginLog.Verbose($"Names updated: {namesUpdated}. {string.Join(", ", neededGearsets.Select(g => g.gearset.Name))}");

                // no change
                if (!namesUpdated)
                    return;

                // update node visibility
                if (neededGearsets.Count == 0)
                {
                    setNodeVisibility(false);
                    return;
                };
                setNodeVisibility(true);

                var addonPtr = (AtkUnitBase*)gameGui.GetAddonByName(AddonName);

                if (addonPtr == null || !addonPtr->IsVisible)
                    return;

                var itemNameIsTwoLines = isItemNameTwoLines(addonPtr);

                // update the custom node with new information
                updateCustomNode(addonPtr, itemNameIsTwoLines);
            }
            catch (Exception e)
            {
                pluginLog.Error(e, "Failed to handle PostRequestedUpdate event");
            }
        }

        public SeString usedInSeString(
            TextNode node,
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

            // if only 2 or less gearsets need, make max gearset length longer to better use the space
            var maxGearsetNameLength = neededGearsets.Count > 2
                ? CustomNodeMaxGearsetNameLength
                : CustomNodeGearsetNameLengthShortList;

            // add gearset names to output string
            for (var i = 0; i < neededGearsets.Count; i++)
            {
                var fullGearsetName = neededGearsets[i].gearset.Name;
                var gearsetCount = $" ({neededGearsets[i].countNeeded})";

                // truncate name if its too long
                var outputGearsetName =
                    fullGearsetName.Length > maxGearsetNameLength - gearsetCount.Length
                    ? fullGearsetName[..(maxGearsetNameLength - (2 + gearsetCount.Length))] + truncateStr
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
                    node.InternalNode->GetTextDrawSize(&textWidth, &textHeight, newOutputPtr);
                }

                if (textWidth + separatorLength <= maxLength)
                { // enough space, include it
                    neededStrings.Add((outputGearsetName, gearsetCount, nextSeparator));
                    outputStr = newOutputStr;
                }
                else
                { // not enough space, truncate and end
                    neededStrings.Add((string.Empty, string.Empty, $"+{neededGearsets.Count - i}{endStr}"));
                    break;
                }
            }

            var outputBuilder = new SeStringBuilder();
            outputBuilder.AddUiForeground(CustomNodeNormalTextColorType);

            foreach (var (gearsetName, gearsetCount, separator) in neededStrings)
            {
                outputBuilder.AddText(gearsetName);
                if (gearsetCount != string.Empty)
                {
                    outputBuilder.AddUiForeground(CustomNodeCountTextColorType);
                    outputBuilder.AddText(gearsetCount);
                    outputBuilder.AddUiForegroundOff();
                }
                outputBuilder.AddText(separator);
            }

            return outputBuilder.Build();
        }

        private void setNodeVisibility(bool setVisible)
        {
            var customTextNode = CustomNodes.Count > 0 ? CustomNodes.First().Value.Node : null;
            if (customTextNode == null)
                return; // doesn't exist, nothing to hide

            var addon = (AtkUnitBase*)gameGui.GetAddonByName(AddonName);
            if (addon == null)
                return; // addon doesn't exist somehow

            var parentNode = addon->GetNodeById(AddonParentNodeId);
            if (parentNode == null)
                return; // parent node doesn't exist somehow

            setNodeNeededMark(parentNode, setVisible ? TextNodeColor : null, false, true);
        }

        private bool updateNeededGearsetNames(uint itemId)
        {
            // get the itemRequirements for this item
            var itemRequirements = gearsetsService.GetItemRequirements(
                itemId,
                includeObtainable: true,
                includeCollectedPrereqs: true
                );

            var newNeededGearsets = itemRequirements
                .GroupBy(requirement => requirement.Gearset)
                .Select(group => (Gearset: group.Key, countNeeded: group.Count()))
                .ToList();

            if (
                newNeededGearsets.Count == neededGearsets.Count
                && newNeededGearsets.SequenceEqual(neededGearsets)
                )
                return false;

            neededGearsets = newNeededGearsets;
            return true;
        }

        private unsafe bool isItemNameTwoLines(AtkUnitBase* addon)
        {
            // get if the item name is split over two lines or not
            var itemNameTextNode = addon->GetTextNodeById(AddonItemNameTextNodeId);
            var itemName = SeString.Parse((byte*)itemNameTextNode->GetText()).TextValue;
            return itemName.Contains("\r\n");
        }

        private unsafe void updateCustomNode(AtkUnitBase* addon, bool itemNameTwoLines)
        {
            // assign custom text to node
            var customTextNode = CustomNodes.Count > 0
                ? (TextNode)CustomNodes.First().Value.Node
                : (TextNode)createCustomNode(addon->GetNodeById(AddonParentNodeId), addon, TextNodeColor);  // doesn't exist, create it

            // get the formatted text to display in the custom node
            var customSeString = usedInSeString(customTextNode, itemNameTwoLines);

            // update node text
            customTextNode.Text = customSeString;
        }

        protected override NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            TextNode? customTextNode = null;
            try
            {
                customTextNode = new TextNode()
                {
                    NodeID = AddonCustomNodeId,
                    // text propreties
                    TextColor = CustomNodeTextColor.ToVector4(),
                    FontSize = CustomNodeTextSize,
                    FontType = CustomNodeTextFont,
                    AlignmentType = AlignmentType.Right,
                    TextFlags = TextFlags.AutoAdjustNodeSize,
                    // location/scale properties
                    Height = CustomNodeHeight,
                    Width = CustomNodeWidth,
                    Position = CustomNodePosition,
                    // general properties
                    NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled,
                };

                nativeController.AttachToAddon(customTextNode, addon, parentNodePtr, NodePosition.AsLastChild);
                return customTextNode;
            }
            catch (Exception ex)
            {
                customTextNode?.Dispose();
                pluginLog.Error(ex, "Failed to initialize custom node");
                return null;
            }
        }

        protected override unsafe void unlinkCustomNode(nint parentNodePtr, NodeBase node)
        {
            var addon = gameGui.GetAddonByName(AddonName);

            if (addon == nint.Zero)
                return;

            if (parentNodePtr == nint.Zero)
                return;

            nativeController.DetachFromAddon(node, (AtkUnitBase*)addon);
        }
    }
}
