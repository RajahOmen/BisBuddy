using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    public abstract class AddonEventListener : EventListener
    {
        private readonly List<nint> customNodes = [];
        private readonly List<nint> highlightedNodes = [];
        protected IReadOnlyList<nint> CustomNodes => customNodes.AsReadOnly();
        public virtual uint AddonCustomNodeId => 420;
        public abstract string AddonName { get; }
        private bool allNodesUnmarked = false;

        protected AddonEventListener(Plugin plugin, bool configBool) : base(plugin)
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
            {
                UiHelper.SetNodeColor((AtkResNode*)node, Plugin.Configuration.HighlightColor, true);
            }
        }

        private void handlePreFinalize(AddonEvent type, AddonArgs args)
        {
            // ensure all nodes are destroyed before finalizing
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

            UiHelper.SetNodeColor(node, color, true);

            return true;
        }

        private unsafe bool setCustomNodeVisibility(AtkResNode* parentNode, bool toEnable)
        {
            var customNode = customNodes.Count > 0 ? getCustomNodeByParent(parentNode) : null;
            if (customNode == null) // create if doesn't exist & should be enabled
            {
                if (!toEnable) return false; // no need to create a node if it's not going to be enabled
                Services.Log.Verbose($"Creating custom node \"{AddonCustomNodeId}\" (parent node \"{parentNode->NodeId}\") in \"{AddonName}\"");
                customNode = (AtkResNode*)createCustomNode((nint)parentNode);
            }

            allNodesUnmarked &= !toEnable; // if any node is enabled, at least one node is marked

            UiHelper.SetNodeColor(customNode, Plugin.Configuration.HighlightColor, false);

            if (customNode->IsVisible() == toEnable) return false;

            customNode->ToggleVisibility(toEnable);
            return true;
        }

        protected unsafe void setNodeNeededMark(AtkResNode* parentNode, bool toEnable, bool highlightParent, bool useCustomNode)
        {
            if (parentNode == null) return; // node parent is null

            var changeMade = false;

            // normal highlighting logic
            if (highlightParent) changeMade = setAddGreen(parentNode, toEnable);

            // custom node logic
            if (useCustomNode) changeMade |= setCustomNodeVisibility(parentNode, toEnable);

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

                    Services.Log.Verbose($"removing color from {node->NodeId}");

                    if (node != null)
                        setAddGreen(node, false);
                }
                if (highlightedNodesCount > 0)
                    Services.Log.Verbose($"Unhighlighted all {highlightedNodesCount} node(s) in \"{AddonName}\"");

                for (var i = 0; i < customNodes.Count; i++)
                {
                    var customNode = (AtkResNode*)customNodes[i];
                    if (customNode == null) continue;
                    if (!customNode->IsVisible()) continue;
                    customNode->ToggleVisibility(false);
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
