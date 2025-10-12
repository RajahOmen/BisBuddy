using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class ItemFinderService(ITypedLogger<ItemFinderService> logger) : IItemFinderService
    {
        private readonly ITypedLogger<ItemFinderService> logger = logger;

        public unsafe void SearchForItem(uint itemId, bool includeHQAndCollectibles = true)
        {
            try
            {
                logger.Info($"Searching for item \"{itemId}\"");
                ItemFinderModule.Instance()->SearchForItem(itemId, includeHQAndCollectibles);
            } catch (Exception ex)
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
