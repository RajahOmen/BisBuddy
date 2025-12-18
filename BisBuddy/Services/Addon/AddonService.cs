using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
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
        private readonly IGameGui gameGui = dependencies.GameGui;
        protected readonly ITypedLogger<T> logger = dependencies.logger;
        protected readonly IFramework framework = dependencies.framework;
        protected readonly IAddonLifecycle addonLifecycle = dependencies.AddonLifecycle;
        protected readonly IGearsetsService gearsetsService = dependencies.GearsetsService;
        protected readonly IItemDataService itemDataService = dependencies.ItemDataService;
        protected readonly IConfigurationService configurationService = dependencies.ConfigurationService;
        protected readonly IDebugService debugService = dependencies.DebugService;

        private static readonly HighlightColor NullColor = new(0.0f, 0.0f, 0.0f, 0.393f);
        private readonly Dictionary<nint, (NodeBase Node, HighlightColor Color)> customNodes = [];
        private readonly Dictionary<nint, HighlightColor> highlightedNodes = [];
        protected IReadOnlyDictionary<nint, (NodeBase Node, HighlightColor Color)> CustomNodes => customNodes;

        public bool IsEnabled { get; private set; } = false;
        public abstract string AddonName { get; }
        public bool IsAddonVisible
        {
            get
            {
                debugService.AssertMainThreadDebug();

                var addon = AddonPtr;
                return addon.IsReady && addon.IsVisible;
            }
        }
        public bool IsAddonNull
        {
            get
            {
                debugService.AssertMainThreadDebug();

                return AddonPtr.IsNull;
            }
        }
        public unsafe IReadOnlyCollection<(uint, NodeHighlightType, HighlightColor)> NodeHighlights
        {
            get
            {
                debugService.AssertMainThreadDebug();

                var nodes = new List<(uint, NodeHighlightType, HighlightColor)>();
                foreach (var (nodePtr, highlightInfo) in customNodes)
                {
                    var parentNodeId = ((AtkResNode*)nodePtr)->NodeId;
                    nodes.Add((parentNodeId, NodeHighlightType.Custom, highlightInfo.Color));
                }
                foreach (var (nodePtr, color) in highlightedNodes)
                {
                    var nodeId = ((AtkResNode*)nodePtr)->NodeId;
                    nodes.Add((nodeId, NodeHighlightType.Native, color));
                }

                return nodes;
            }
        }

        protected abstract unsafe NodeBase initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color);

        protected abstract void registerAddonListeners();
        protected abstract void unregisterAddonListeners();
        protected abstract bool isEnabledFromConfig { get; }


        protected AtkUnitBasePtr AddonPtr
        {
            get
            {
                debugService.AssertMainThreadDebug();
                return gameGui.GetAddonByName(AddonName);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // ensure listening status remains updated with user configuration
            configurationService.OnConfigurationChange += updateListeningStatusFromConfig;

            // set initial listening status to configs initialized value
            updateListeningStatusFromConfig();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            setListeningStatus(toEnable: false);
            configurationService.OnConfigurationChange -= updateListeningStatusFromConfig;
            return Task.CompletedTask;
        }

        private void updateListeningStatusFromConfig(bool effectsAssignments = false)
            => setListeningStatus(isEnabledFromConfig);

        private void setListeningStatus(bool toEnable)
        {
            if (toEnable && !IsEnabled) // If we want to enable and it's not enabled
                registerListeners();
            else if (!toEnable && IsEnabled) // If we want to disable and it's enabled
                unregisterListeners();
            // Otherwise, do nothing
        }

        private void registerListeners()
        {
            try
            {
                gearsetsService.OnGearsetsChange += handleUpdateHighlightColor;
                configurationService.OnConfigurationChange += handleUpdateHighlightColor;
                addonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, handlePreFinalize);
                registerAddonListeners();

                IsEnabled = true;
                logger.Verbose($"Registered listeners");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to register listeners");
            }
        }

        private unsafe void unregisterListeners()
        {
            debugService.AssertMainThreadDebug();

            try
            {
                if (IsEnabled) // only perform these if it is enabled
                {
                    gearsetsService.OnGearsetsChange -= handleUpdateHighlightColor;
                    configurationService.OnConfigurationChange -= handleUpdateHighlightColor;
                    addonLifecycle.UnregisterListener(handlePreFinalize);
                    unregisterAddonListeners();
                }
                unmarkNodes();
                destroyNodes();

                IsEnabled = false;
                logger.Verbose($"Unregistered listeners");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to unregister listeners");
            }
        }

        private unsafe void handleUpdateHighlightColor()
        {
            framework.RunOnFrameworkThread(() =>
            {
                if (AddonPtr.IsNull && highlightedNodes.Count > 0)
                    throw new Exception($"Addon \"{AddonName}\" is not loaded, cannot update \"{highlightedNodes.Count}\" node highlight colors");

                // update colors for existing atk nodes that was just highlighted
                foreach (var nodeData in highlightedNodes)
                    nodeData.Value.ColorExistingNode((AtkResNode*)nodeData.Key);

                // update colors for nodes that we added
                foreach (var nodeData in customNodes.Values)
                    nodeData.Color.ColorCustomNode(nodeData.Node, configurationService.BrightListItemHighlighting);
            });
        }

        private unsafe void handleUpdateHighlightColor(bool effectsAssignments = false)
            => handleUpdateHighlightColor();

        private unsafe void handlePreFinalize(AddonEvent type, AddonArgs args)
        {
            debugService.AssertMainThreadDebug();

            // ensure all nodes are destroyed before finalizing
            unmarkNodes();
            destroyNodes();
        }

        protected unsafe NodeBase createCustomNode(AtkResNode* parentNode, AtkUnitBase* addon, HighlightColor color)
        {
            debugService.AssertMainThreadDebug();

            logger.Verbose(
                $"Creating custom node (parent node \"{parentNode->NodeId}\") " +
                $"in \"{AddonName}\" with color {color.BaseColor}"
                );

            var customNode = initializeCustomNode(parentNode, addon, color);

            try
            {
                customNode.MarkDirty();
                if (parentNode->GetNodeType() is NodeType.Component)
                    customNode.AttachNode((AtkComponentNode*)parentNode);
                else
                    customNode.AttachNode(parentNode);
                customNodes.Add((nint)parentNode, (customNode, color));
                return customNode;
            }
            catch (Exception ex)
            {
                customNode.Dispose();
                customNode = null;
                logger.Error(ex, $"Failed to create custom node in \"{AddonName}\"");
                throw;
            }
        }

        private unsafe bool setAddColor(AtkResNode* node, HighlightColor? color)
        {
            debugService.AssertMainThreadDebug();

            if (!AddonPtr.IsReady)
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
            debugService.AssertMainThreadDebug();

            if (!customNodes.TryGetValue((nint)parentNode, out var customNodeData))
            {
                // no need to create a node if it's not going to be enabled
                if (color is null)
                    return false;

                var addon = (AtkUnitBase*)AddonPtr.Address;
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
            debugService.AssertMainThreadDebug();

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
            debugService.AssertMainThreadDebug();

            try
            {
                var highlightedNodesCount = highlightedNodes.Count;

                // only unhighlight nodes if the addon is loaded
                if (!AddonPtr.IsNull)
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

        private unsafe void destroyNodes()
        {
            var count = customNodes.Count;
            foreach (var customNodeEntry in customNodes)
            {
                var node = customNodeEntry.Value.Node;
                node.Dispose();
                node = null;
                customNodes.Remove(customNodeEntry.Key);
            }

            if (count > 0)
                logger.Verbose($"Destroyed all {count} custom nodes in \"{AddonName}\"");
        }
    }

    public interface IAddonEventListener : IHostedService
    {
        public bool IsEnabled { get; }
        public string AddonName { get; }
        public bool IsAddonVisible { get; }
        public bool IsAddonNull { get; }
        public IReadOnlyCollection<(uint NodeId, NodeHighlightType Type, HighlightColor Color)> NodeHighlights { get; }
    }
}
