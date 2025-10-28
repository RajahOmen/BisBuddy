using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;

namespace BisBuddy.Services
{
    public class ItemFinderService(
        ITypedLogger<ItemFinderService> logger,
        IDebugService debugService
        ) : IItemFinderService
    {
        private readonly ITypedLogger<ItemFinderService> logger = logger;
        private readonly IDebugService debugService = debugService;

        public unsafe void SearchForItem(uint itemId, bool includeHQAndCollectibles = true)
        {
            debugService.AssertMainThreadDebug();
            try
            {
                logger.Info($"Searching for item \"{itemId}\"");
                ItemFinderModule.Instance()->SearchForItem(itemId, includeHQAndCollectibles);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error searching for \"{itemId}\"");
            }
        }
    }

    public interface IItemFinderService
    {
        public void SearchForItem(uint itemId, bool includeHQAndCollectibles = true);
    }
}
