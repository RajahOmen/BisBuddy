using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    public abstract class AddonEventListener : EventListener
    {
        private static readonly HighlightColor NullColor = new(0.0f, 0.0f, 0.0f, 0.393f);
        private readonly Dictionary<nint, (NodeBase Node, HighlightColor Color)> customNodes = [];
        private readonly Dictionary<nint, HighlightColor> highlightedNodes = [];
        protected IReadOnlyDictionary<nint, (NodeBase Node, HighlightColor Color)> CustomNodes => customNodes;
        public virtual uint AddonCustomNodeId => 420000;
        public abstract string AddonName { get; }

        // for lists that scroll, to avoid them rendering off the bottom
        protected abstract float CustomNodeMaxY { get; }

        protected AddonEventListener(Plugin plugin, bool configBool) : base(plugin)
        {
            SetListeningStatus(configBool);
        }

        protected abstract unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color);

        protected abstract unsafe void unlinkCustomNode(nint parentNodePtr, NodeBase node);

        protected abstract void registerAddonListeners();
        protected abstract void unregisterAddonListeners();

        protected override void dispose()
        {
            // treat disposing the same as unregistering
            unregister();
        }

        protected override void register()
        {
            Plugin.OnGearsetsUpdate += handleUpdateHighlightColor;
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
            registerAddonListeners();
        }

        protected override unsafe void unregister()
        {
            if (IsEnabled) // only perform these if it is enabled
            {
                Plugin.OnGearsetsUpdate -= handleUpdateHighlightColor;
                Services.AddonLifecycle.UnregisterListener(handlePreFinalize);
                unregisterAddonListeners();
            }
            unmarkNodes();
            destroyNodes();
        }

        private unsafe void handleUpdateHighlightColor()
        {
            // update colors for existing atk nodes that was just highlighted
            foreach (var nodeData in highlightedNodes)
                nodeData.Value.ColorExistingNode((AtkResNode*)nodeData.Key);

            // update colors for nodes that we added
            foreach (var nodeData in customNodes.Values)
                nodeData.Color.ColorCustomNode(nodeData.Node, Plugin.Configuration.BrightListItemHighlighting);
        }

        private unsafe void handlePreFinalize(AddonEvent type, AddonArgs args)
        {
            // ensure all nodes are destroyed before finalizing
            unmarkNodes();
            destroyNodes();
        }

        protected unsafe NodeBase createCustomNode(AtkResNode* parentNode, AtkUnitBase* addon, HighlightColor color)
        {
            Services.Log.Verbose(
                $"Creating custom node \"{AddonCustomNodeId}\" (parent node \"{parentNode->NodeId}\") " +
                $"in \"{AddonName}\" with color {color.BaseColor}"
                );

            var customNode = initializeCustomNode(parentNode, addon, color)
                ?? throw new Exception($"Failed to create custom node for \"{AddonName}\"");

            customNodes.Add((nint)parentNode, (customNode, color));
            return customNode;
        }

        private unsafe bool setAddGreen(AtkResNode* node, HighlightColor? color)
        {
            var nodeHighlighted = highlightedNodes.TryGetValue((nint)node, out var currentColor);

            // unhighlighting node
            if (nodeHighlighted && color is null)
            {
                highlightedNodes.Remove((nint)node);
                NullColor.ColorExistingNode(node);
                return true;
            }
            // highlighting currently-unhighlighted node
            else if (!nodeHighlighted && color is not null)
            {
                highlightedNodes.Add((nint)node, color);
                color.ColorExistingNode(node);
                return true;
            }
            // changing highlight color
            else if (nodeHighlighted && color is not null && !color.Equals(currentColor))
            {
                Services.Log.Verbose($"Changing node \"{node->NodeId}\" color from {currentColor!.BaseColor} to {color.BaseColor}");
                color.ColorExistingNode(node);
                highlightedNodes[(nint)node] = color;
                return true;
            }

            // no change to this node
            return false;
        }

        private unsafe bool setCustomNodeVisibility(AtkResNode* parentNode, HighlightColor? color)
        {
            if (!customNodes.TryGetValue((nint)parentNode, out var customNodeData))
            {
                // no need to create a node if it's not going to be enabled
                if (color is null)
                    return false;

                var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                customNodeData = (createCustomNode(parentNode, addon, color), color);
            }

            // update color if necessary
            if (color is not null && !customNodeData.Color.Equals(color))
            {
                Services.Log.Verbose(
                    $"Changing custom node \"{customNodeData.Node.NodeID}\" color " +
                    $"from {customNodeData.Color.BaseColor} to {color.BaseColor}"
                    );
                color.ColorCustomNode(customNodeData.Node, Plugin.Configuration.BrightListItemHighlighting);
                customNodeData.Color = color;
                customNodes[(nint)parentNode] = customNodeData;
            }

            // only make visible if color to mark is provided & custom node within min and max values
            var makeVisible = color is not null
                && (parentNode->Y + customNodeData.Node.Height) >= (customNodeData.Node.Height / 2)
                && (parentNode->Y + (customNodeData.Node.Height / 2)) <= CustomNodeMaxY;

            // update position if showing the node
            if (makeVisible)
            {
                // move node to be positioned where it should be
                customNodeData.Node.ScreenX = parentNode->ScreenX + customNodeData.Node.X;
                customNodeData.Node.ScreenY = parentNode->ScreenY + customNodeData.Node.Y;
            }

            if (customNodeData.Node.IsVisible == makeVisible)
                return false;

            customNodeData.Node.IsVisible = makeVisible;
            return true;
        }

        protected unsafe void setNodeNeededMark(AtkResNode* parentNode, HighlightColor? color, bool highlightParent, bool useCustomNode)
        {
            // node parent is null
            if (parentNode == null)
                return;

            var changeMade = false;

            // normal highlighting logic
            if (highlightParent)
                changeMade = setAddGreen(parentNode, color);

            // custom node logic
            if (useCustomNode)
                changeMade |= setCustomNodeVisibility(parentNode, color);

            if (changeMade)
                Services.Log.Verbose(
                    $"Set node \"{parentNode->NodeId}\" in \"{AddonName}\" marking to " +
                    $"{(color is not null ? $"enabled ({color.BaseColor})" : "disabled")}"
                    );
        }

        protected unsafe void unmarkNodes(AtkResNode* parentNodeFilter = null)
        {
            try
            {
                var highlightedNodesCount = highlightedNodes.Count;
                for (var i = 0; i < highlightedNodesCount; i++)
                {
                    var node = (AtkResNode*)highlightedNodes.First().Key;

                    if (parentNodeFilter != null && parentNodeFilter != node->ParentNode)
                        continue;

                    if (node != null)
                    {
                        setAddGreen(node, null);
                        Services.Log.Verbose($"Unhighlighted {node->NodeId} in \"{AddonName}\"");
                    }
                }

                foreach (var customNodeEntry in customNodes)
                {
                    if (parentNodeFilter != null && parentNodeFilter != ((AtkResNode*)customNodeEntry.Key)->ParentNode)
                        continue;

                    if (customNodeEntry.Value.Node.IsVisible)
                    {
                        customNodeEntry.Value.Node.IsVisible = false;
                        Services.Log.Verbose($"Hid {((AtkResNode*)customNodeEntry.Key)->NodeId}'s custom node in \"{AddonName}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, $"Failed to unmark all nodes in \"{AddonName}\"");
            }
        }

        protected unsafe void destroyNodes()
        {
            var count = customNodes.Count;
            foreach (var customNodeEntry in customNodes)
            {
                unlinkCustomNode(customNodeEntry.Key, customNodeEntry.Value.Node);

                customNodeEntry.Value.Node.Dispose();

                customNodes.Remove(customNodeEntry.Key);
            }

            if (customNodes.Count > 0)
                throw new Exception($"Not all nodes destroyed in \"{AddonName}\". {customNodes.Count} nodes remaining.");

            if (count > 0)
                Services.Log.Verbose($"Destroyed all {count} custom nodes in \"{AddonName}\"");
        }
    }
}
