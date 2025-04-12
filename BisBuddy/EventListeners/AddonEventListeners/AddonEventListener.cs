using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    public abstract class AddonEventListener : EventListener
    {
        private readonly Dictionary<nint, NodeBase> customNodes = [];
        private readonly List<nint> highlightedNodes = [];
        protected IReadOnlyList<NodeBase> CustomNodes => customNodes.Values.ToList().AsReadOnly();
        public virtual uint AddonCustomNodeId => 420000;
        public abstract string AddonName { get; }
        private bool allNodesUnmarked = false;

        protected AddonEventListener(Plugin plugin, bool configBool) : base(plugin)
        {
            SetListeningStatus(configBool);
        }

        public abstract void handleManualUpdate();

        protected abstract unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon);

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
            Plugin.OnGearsetsUpdate += handleManualUpdate;
            Plugin.OnGearsetsUpdate += handleUpdateHighlightColor;
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
            registerAddonListeners();

            // call an update in case the listener is registered while addon is visible
            if (Services.ClientState.IsLoggedIn)
            {
                handleManualUpdate();
            }
        }

        protected override void unregister()
        {
            if (IsEnabled) // only perform these if it is enabled
            {
                Plugin.OnGearsetsUpdate -= handleManualUpdate;
                Plugin.OnGearsetsUpdate -= handleUpdateHighlightColor;
                Services.AddonLifecycle.UnregisterListener(handlePreFinalize);
                unregisterAddonListeners();
            }
            unmarkAllNodes();
            destroyNodes();
        }

        private unsafe void handleUpdateHighlightColor()
        {
            foreach (var node in highlightedNodes)
                UiHelper.SetNodeColor((AtkResNode*)node, Plugin.Configuration.HighlightColor);

            foreach (var customNode in customNodes.Values)
            {
                customNode.AddColor = Plugin.Configuration.CustomNodeAddColor;
                customNode.Alpha = Plugin.Configuration.CustomNodeAlpha;
            }
        }

        private void handlePreFinalize(AddonEvent type, AddonArgs args)
        {
            // ensure all nodes are destroyed before finalizing
            destroyNodes();
        }

        protected unsafe NodeBase createCustomNode(AtkResNode* parentNode, AtkUnitBase* addon)
        {
            Services.Log.Verbose($"Creating custom node \"{AddonCustomNodeId}\" (parent node \"{parentNode->NodeId}\") in \"{AddonName}\"");

            var customNode = initializeCustomNode(parentNode, addon)
                ?? throw new Exception($"Failed to create custom node for \"{AddonName}\"");

            customNodes.Add((nint)parentNode, customNode);
            return customNode;
        }

        private unsafe bool setAddGreen(AtkResNode* node, bool toEnable)
        {
            var nodeHighlighted = highlightedNodes.Contains((nint)node);

            // remove if unhighlighting
            if (highlightedNodes.Contains((nint)node) && !toEnable)
                highlightedNodes.Remove((nint)node);

            // add if highlighting
            if (!highlightedNodes.Contains((nint)node) && toEnable)
                highlightedNodes.Add((nint)node);

            if (nodeHighlighted == toEnable)
                return false; // already in the desired state

            if (toEnable)
                allNodesUnmarked = false; // marking a node here, not all unmarked

            var color = toEnable
                ? Plugin.Configuration.HighlightColor
                : new Vector4(0.0f, 0.0f, 0.0f, 1.0f); // reset color, eqv. to (0, 0, 0, 255)

            UiHelper.SetNodeColor(node, color);

            return true;
        }

        private unsafe bool setCustomNodeVisibility(AtkResNode* parentNode, bool toEnable)
        {
            if (!customNodes.TryGetValue((nint)parentNode, out var customNode))
            {
                // no need to create a node if it's not going to be enabled
                if (!toEnable)
                    return false;

                var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                customNode = createCustomNode(parentNode, addon);
            }

            allNodesUnmarked &= !toEnable; // if any node is enabled, at least one node is marked

            if (customNode.IsVisible == toEnable)
                return false;

            customNode.IsVisible = toEnable;
            return true;
        }

        protected unsafe void setNodeNeededMark(AtkResNode* parentNode, bool toEnable, bool highlightParent, bool useCustomNode)
        {
            // node parent is null
            if (parentNode == null)
                return;

            var changeMade = false;

            // normal highlighting logic
            if (highlightParent)
                changeMade = setAddGreen(parentNode, toEnable);

            // custom node logic
            if (useCustomNode)
                changeMade |= setCustomNodeVisibility(parentNode, toEnable);

            if (changeMade)
                Services.Log.Verbose($"Set node \"{parentNode->NodeId}\" in \"{AddonName}\" marking to {(toEnable ? "enabled" : "disabled")}");
        }

        protected unsafe void unmarkAllNodes()
        {
            if (allNodesUnmarked) return; // already unmarked
            try
            {
                var highlightedNodesCount = highlightedNodes.Count;
                for (var i = 0; i < highlightedNodesCount; i++)
                {
                    var node = (AtkResNode*)highlightedNodes[0];

                    if (node != null)
                        setAddGreen(node, false);
                }
                if (highlightedNodesCount > 0)
                    Services.Log.Verbose($"Unhighlighted all {highlightedNodesCount} node(s) in \"{AddonName}\"");

                foreach (var customNode in customNodes.Values)
                {
                    customNode.IsVisible = false;
                }
                if (customNodes.Count > 0)
                    Services.Log.Verbose($"Hid all {customNodes.Count} custom node(s) in \"{AddonName}\"");
                allNodesUnmarked = true;
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, $"Failed to unmark all nodes in \"{AddonName}\"");
            }
        }

        protected unsafe void destroyNodes()
        {
            var count = customNodes.Count;
            var customNodesList = customNodes.ToList();
            foreach (var customNodeEntry in customNodesList)
            {
                unlinkCustomNode(customNodeEntry.Key, customNodeEntry.Value);

                customNodeEntry.Value.Dispose();

                customNodes.Remove(customNodeEntry.Key);
            }

            if (customNodes.Count > 0)
                throw new Exception($"Not all nodes destroyed in \"{AddonName}\". {customNodes.Count} nodes remaining.");

            if (count > 0)
                Services.Log.Verbose($"Destroyed all {count} custom nodes in \"{AddonName}\"");
        }
    }
}
