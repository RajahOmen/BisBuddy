using BisBuddy.Services.Addon;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class InvalidNodePollerService(
        ITypedLogger<InvalidNodePollerService> logger,
        IFramework framework,
        IEnumerable<IAddonEventListener> listeners
        ) : IInvalidNodePollerService
    {
        private readonly ITypedLogger<InvalidNodePollerService> logger = logger;
        private readonly IFramework framework = framework;
        private readonly IEnumerable<IAddonEventListener> listeners = listeners;

        private const int LogUpdatePeriod = 1000;
        private int currentUpdateTicks = (int)(LogUpdatePeriod * 0.9);

        private void logInvalidNodes(IFramework framework)
        {
            currentUpdateTicks += 1;
            if (currentUpdateTicks < LogUpdatePeriod)
                return;

            logger.Verbose($"Checking for invalid highlight nodes...");

            currentUpdateTicks = 0;

            var invalidNodes = new List<(
            string AddonName,
                bool AddonNull,
                uint NodeId,
                NodeHighlightType NodeType,
                Vector4 Color)
            >();

            foreach (var listener in listeners)
            {
                // can't be invalid if not null and enabled
                if (!listener.IsAddonNull && listener.IsEnabled)
                    continue;

                foreach (var node in listener.NodeHighlights)
                {
                    invalidNodes.Add((
                        listener.AddonName,
                        listener.IsAddonNull,
                        node.NodeId,
                        node.Type, node.Color.BaseColor
                    ));
                }
            }

            if (invalidNodes.Count == 0)
                return;

            logger.Warning("=== Highlight nodes not properly tracked or disposed of ===");
            foreach (var node in invalidNodes)
            {
                var errorTypeStr = node.AddonNull
                    ? "addon is null"
                    : "listener has been disabled";
                logger.Warning($"[{node.AddonName}] {Enum.GetName(node.NodeType)} Node {node.NodeId} exists while {errorTypeStr} (color: {node.Color})");
            }
            logger.Warning("=== Please report these errors to the plugin developer in BisBuddy thread in the Dalamud discord ===");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            framework.Update += logInvalidNodes;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            framework.Update -= logInvalidNodes;
            return Task.CompletedTask;
        }
    }

    public interface IInvalidNodePollerService : IHostedService
    {

    }
}
