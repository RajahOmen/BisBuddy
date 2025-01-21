using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    internal abstract class AddonEventListenerBase : EventListenerBase
    {
        public static readonly short HighlightStrength = 100;
        private readonly List<nint> customNodes = [];
        private readonly List<nint> highlightedNodes = [];
        protected IReadOnlyList<nint> CustomNodes => customNodes.AsReadOnly();
        public virtual uint AddonCustomNodeId => 420;
        public abstract string AddonName { get; }
        private bool allNodesUnmarked = false;

        protected AddonEventListenerBase(Plugin plugin, bool configBool) : base(plugin)
        {
            SetListeningStatus(configBool);
        }

        public abstract void handleManualUpdate();

        protected abstract unsafe void unlinkCustomNode(nint nodePtr);

        protected abstract unsafe nint initializeCustomNode(nint parentNodePtr);

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
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
            registerAddonListeners();

            // call an update in case the listener is registered while addon is visible
            handleManualUpdate();
        }

        protected override void unregister()
        {
            if (IsEnabled) // only perform these if it is enabled
            {
                Plugin.OnGearsetsUpdate -= handleManualUpdate;
                Services.AddonLifecycle.UnregisterListener(handlePreFinalize);
                unregisterAddonListeners();
            }
            unmarkAllNodes();
            destroyNodes();
        }

        protected unsafe AtkResNode* getCustomNodeByParent(AtkResNode* parent)
        {
            for (var i = 0; i < customNodes.Count; i++)
            {
                var customHighlightNode = (AtkResNode*)customNodes[i];
                if (customHighlightNode->ParentNode == parent)
                {
                    return customHighlightNode;
                }
            }

            return null;
        }

        private void handlePreFinalize(AddonEvent type, AddonArgs args)
        {
            // ensure all nodes are destroyed before finalizing
            destroyNodes();
        }

        protected nint createCustomNode(nint parentNode)
        {
            var customNode = initializeCustomNode(parentNode);

            if (customNode == nint.Zero)
            {
                throw new Exception($"Failed to create custom node for \"{AddonName}\"");
            }

            customNodes.Add(customNode);
            return customNode;
        }

        private unsafe void setAddGreen(AtkResNode* node, bool toEnable)
        {
            var nodeHighlighted = node->AddGreen == HighlightStrength;

            // remove if unhighlighting
            if (highlightedNodes.Contains((nint)node) && !toEnable)
                highlightedNodes.Remove((nint)node);

            // add if highlighting
            if (!highlightedNodes.Contains((nint)node) && toEnable)
                highlightedNodes.Add((nint)node);

            if (nodeHighlighted == toEnable) return; // already in the desired state

            if (toEnable) allNodesUnmarked = false; // marking a node here, not all unmarked

            var value = toEnable ? HighlightStrength : (short)0;
            Services.Log.Verbose($"Setting node {node->NodeId} AddGreen to \"{value}\" in \"{AddonName}\"");
            node->AddGreen = value;
        }

        private unsafe void setCustomNodeVisibility(AtkResNode* parentNode, bool toEnable)
        {
            var customNode = customNodes.Count > 0 ? getCustomNodeByParent(parentNode) : null;
            if (customNode == null) // create if doesn't exist & should be enabled
            {
                if (!toEnable) return; // no need to create a node if it's not going to be enabled
                customNode = (AtkResNode*)createCustomNode((nint)parentNode);
                Services.Log.Verbose($"Created custom node {customNode->NodeId} (parent node {parentNode->NodeId}) in \"{AddonName}\"");
                customNode->ToggleVisibility(toEnable);
            }

            allNodesUnmarked &= !toEnable; // if any node is enabled, at least one node is marked

            if (customNode->IsVisible() == toEnable) return;

            customNode->ToggleVisibility(toEnable);
            Services.Log.Verbose($"Set custom node {customNode->NodeId} (parent node {parentNode->NodeId}) visibility to \"{toEnable}\" in \"{AddonName}\"");
        }

        protected unsafe void setNodeNeededMark(AtkResNode* parentNode, bool toEnable, bool highlightParent, bool useCustomNode)
        {
            if (parentNode == null) return; // node parent is null

            // normal highlighting logic
            if (highlightParent) setAddGreen(parentNode, toEnable);

            // custom node logic
            if (useCustomNode) setCustomNodeVisibility(parentNode, toEnable);
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

                    highlightedNodes.RemoveAt(0);

                    if (node == null) continue;

                    setAddGreen(node, false);
                }
                Services.Log.Verbose($"Unhighlighted all {highlightedNodesCount} nodes in \"{AddonName}\"");
                for (var i = 0; i < customNodes.Count; i++)
                {
                    var customNode = (AtkResNode*)customNodes[i];
                    if (customNode == null) continue;
                    if (!customNode->IsVisible()) continue;
                    customNode->ToggleVisibility(false);
                }
                Services.Log.Verbose($"Hid all {customNodes.Count} custom nodes in \"{AddonName}\"");
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
            for (var i = 0; i < count; i++)
            {
                var nodeInfo = customNodes[0];
                var customNode = (AtkResNode*)nodeInfo;
                if (customNode == null) continue; // node is null

                unlinkCustomNode(nodeInfo); // if the node is still linked, unlink it

                UiHelper.FreeNode(customNode);

                customNodes.RemoveAt(0);
            }

            if (customNodes.Count > 0)
            {
                throw new Exception($"Not all nodes destroyed in \"{AddonName}\". {customNodes.Count} nodes remaining.");
            }

            if (count > 0)
            {
                Services.Log.Verbose($"Destroyed all {count} custom nodes in \"{AddonName}\"");
            }
        }
    }
}
