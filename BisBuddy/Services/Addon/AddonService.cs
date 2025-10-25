using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.System;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services.Addon
{
    public abstract class AddonService<T>(
        AddonServiceDependencies<T> dependencies
        ) : IAddonEventListener where T : class
    {
        protected readonly ITypedLogger<T> logger = dependencies.logger;
        protected readonly IAddonLifecycle addonLifecycle = dependencies.AddonLifecycle;
        protected readonly IGameGui gameGui = dependencies.GameGui;
        protected readonly NativeController nativeController = dependencies.NativeController;
        protected readonly IGearsetsService gearsetsService = dependencies.GearsetsService;
        protected readonly IItemDataService itemDataService = dependencies.ItemDataService;
        protected readonly IConfigurationService configurationService = dependencies.ConfigurationService;

        private static readonly HighlightColor NullColor = new(0.0f, 0.0f, 0.0f, 0.393f);
        private readonly Dictionary<nint, (NodeBase Node, HighlightColor Color)> customNodes = [];
        private readonly Dictionary<nint, HighlightColor> highlightedNodes = [];
        protected IReadOnlyDictionary<nint, (NodeBase Node, HighlightColor Color)> CustomNodes => customNodes;

        protected bool isEnabled { get; private set; } = false;
        public abstract string AddonName { get; }

        // for lists that scroll, to avoid them rendering off the bottom
        protected abstract float CustomNodeMaxY { get; }

        protected abstract unsafe NodeBase initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color);

        protected abstract void registerAddonListeners();
        protected abstract void unregisterAddonListeners();
        protected abstract void updateListeningStatus(bool effectsAssignments);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // ensure listening status remains updated with user configuration
            configurationService.OnConfigurationChange += updateListeningStatus;

            // set initial listening status to configs initialized value
            updateListeningStatus(false);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            setListeningStatus(false);
            configurationService.OnConfigurationChange -= updateListeningStatus;
            return Task.CompletedTask;
        }

        protected void setListeningStatus(bool toEnable)
        {
            if (toEnable && !isEnabled) // If we want to enable and it's not enabled
                registerListeners();
            else if (!toEnable && isEnabled) // If we want to disable and it's enabled
                unregisterListeners();
            // Otherwise, do nothing
        }

        protected void registerListeners()
        {
            try
            {
                gearsetsService.OnGearsetsChange += handleUpdateHighlightColor;
                configurationService.OnConfigurationChange += handleUpdateHighlightColor;
                addonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
                registerAddonListeners();

                isEnabled = true;
                logger.Verbose($"Registered listeners");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to register listeners");
            }
        }

        protected unsafe void unregisterListeners()
        {
            try
            {
                if (isEnabled) // only perform these if it is enabled
                {
                    gearsetsService.OnGearsetsChange -= handleUpdateHighlightColor;
                    configurationService.OnConfigurationChange -= handleUpdateHighlightColor;
                    addonLifecycle.UnregisterListener(handlePreFinalize);
                    unregisterAddonListeners();
                }
                unmarkNodes();
                destroyNodes();

                isEnabled = false;
                logger.Verbose($"Unregistered listeners");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to unregister listeners");
            }
        }

        private unsafe void handleUpdateHighlightColor()
        {
            if (gameGui.GetAddonByName(AddonName).IsNull && highlightedNodes.Count > 0)
                throw new Exception($"Addon \"{AddonName}\" is not loaded, cannot update \"{highlightedNodes.Count}\" node highlight colors");

            // update colors for existing atk nodes that was just highlighted
            foreach (var nodeData in highlightedNodes)
                nodeData.Value.ColorExistingNode((AtkResNode*)nodeData.Key);

            // update colors for nodes that we added
            foreach (var nodeData in customNodes.Values)
                nodeData.Color.ColorCustomNode(nodeData.Node, configurationService.BrightListItemHighlighting);
        }

        private unsafe void handleUpdateHighlightColor(bool effectsAssignments = false)
            => handleUpdateHighlightColor();

        private unsafe void handlePreFinalize(AddonEvent type, AddonArgs args)
        {
            // ensure all nodes are destroyed before finalizing
            unmarkNodes();
            destroyNodes();
        }

        protected unsafe NodeBase createCustomNode(AtkResNode* parentNode, AtkUnitBase* addon, HighlightColor color)
        {
            logger.Verbose(
                $"Creating custom node (parent node \"{parentNode->NodeId}\") " +
                $"in \"{AddonName}\" with color {color.BaseColor}"
                );

            var customNode = initializeCustomNode(parentNode, addon, color);

            try
            {
                customNode.MarkDirty();
                if (parentNode->GetNodeType() is NodeType.Component)
                    nativeController.AttachNode(customNode, (AtkComponentNode*)parentNode);
                else
                    nativeController.AttachNode(customNode, parentNode);
                customNodes.Add((nint)parentNode, (customNode, color));
                return customNode;
            }
            catch (Exception ex)
            {
                nativeController.DisposeNode(ref customNode);
                logger.Error(ex, $"Failed to create custom node in \"{AddonName}\"");
                throw;
            }
        }

        private unsafe bool setAddColor(AtkResNode* node, HighlightColor? color)
        {
            if (gameGui.GetAddonByName(AddonName).IsNull)
                throw new Exception($"Addon \"{AddonName}\" is not loaded, cannot set node highlight color");

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
                logger.Verbose($"Changing node \"{node->NodeId}\" color from {currentColor!.BaseColor} to {color.BaseColor}");
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

                var addon = (AtkUnitBase*)gameGui.GetAddonByName(AddonName).Address;
                customNodeData = (createCustomNode(parentNode, addon, color), color);
            }

            // update color if necessary
            if (color is not null && !customNodeData.Color.Equals(color))
            {
                logger.Verbose(
                    $"Changing custom node \"{customNodeData.Node.NodeId}\" color " +
                    $"from {customNodeData.Color.BaseColor} to {color.BaseColor}"
                    );
                color.ColorCustomNode(customNodeData.Node, configurationService.BrightListItemHighlighting);
                customNodeData.Color = color;
                customNodes[(nint)parentNode] = customNodeData;
            }

            // only make visible if color to mark is provided & custom node within min and max values
            var makeVisible = color is not null;

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
                changeMade = setAddColor(parentNode, color);

            // custom node logic
            if (useCustomNode)
                changeMade |= setCustomNodeVisibility(parentNode, color);

            if (changeMade)
                logger.Verbose(
                    $"Set node \"{parentNode->NodeId}\" in \"{AddonName}\" marking to " +
                    $"{(color is not null ? $"enabled ({color.BaseColor})" : "disabled")}"
                    );
        }

        protected unsafe void unmarkNodes(AtkResNode* parentNodeFilter = null)
        {
            try
            {
                var highlightedNodesCount = highlightedNodes.Count;

                // only unhighlight nodes if the addon is loaded
                if (!gameGui.GetAddonByName(AddonName).IsNull)
                {
                    for (var i = 0; i < highlightedNodesCount; i++)
                    {
                        var node = (AtkResNode*)highlightedNodes.First().Key;

                        if (parentNodeFilter != null && parentNodeFilter != node->ParentNode)
                            continue;

                        if (node != null)
                        {
                            setAddColor(node, null);
                            logger.Verbose($"Unhighlighted {node->NodeId} in \"{AddonName}\"");
                        }
                    }
                }

                foreach (var customNodeEntry in customNodes)
                {
                    if (parentNodeFilter != null && parentNodeFilter != ((AtkResNode*)customNodeEntry.Key)->ParentNode)
                        continue;

                    if (customNodeEntry.Value.Node.IsVisible)
                    {
                        customNodeEntry.Value.Node.IsVisible = false;
                        logger.Verbose($"Hid {((AtkResNode*)customNodeEntry.Key)->NodeId}'s custom node in \"{AddonName}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, $"Failed to unmark all nodes in \"{AddonName}\"");
            }
            finally
            {
                if (parentNodeFilter is null)
                    highlightedNodes.Clear();
            }
        }

        protected unsafe void destroyNodes()
        {
            var count = customNodes.Count;
            foreach (var customNodeEntry in customNodes)
            {
                var node = customNodeEntry.Value.Node;
                nativeController.DisposeNode(ref node);
                customNodes.Remove(customNodeEntry.Key);
            }

            if (count > 0)
                logger.Verbose($"Destroyed all {count} custom nodes in \"{AddonName}\"");
        }
    }

    public interface IAddonEventListener : IHostedService
    {

    }
}
