using Lumina.Excel;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Items
{
    public partial class ItemData
    {
        // to ignore shop costs for common currencies (tomestones, MGP, etc.)
        // doesn't cover currencies added later
        private static readonly int MaxCurrencyId = 99;
        // Max cost item quantity is limited to limit size of graph for assignment
        private static readonly int MaxCostQuantity = 20;

        private static List<SpecialShop> getRelevantShops(ExcelSheet<SpecialShop> shopSheet)
        {
            var shops = new List<SpecialShop>();

            foreach (var shop in shopSheet) // check all SpecialShops
                shops.Add(shop);

            return shops;
        }

        private static ILookup<uint, uint> generateItemsCoffers()
        {
            try
            {
                var lines = CsvLoader.LoadResource<ItemSupplement>(
                    CsvLoader.ItemSupplementResourceName,
                    true,
                    out var failedLines,
                    out var exceptions,
                    Services.DataManager.GameData,
                    Services.DataManager.GameData.Options.DefaultExcelLanguage
                    );
                if (failedLines.Count != 0)
                {
                    foreach (var failedLine in failedLines)
                    {
                        Services.Log.Error("Failed to load line from " + CsvLoader.ItemSupplementResourceName + ": " + failedLine);
                    }
                }

                return lines
                    .Where(item => item.ItemSupplementSource == ItemSupplementSource.Loot)
                    .ToLookup(i => i.ItemId, i => i.SourceItemId);
            }
            catch (Exception e)
            {
                Services.Log.Error("Failed to generate coffers using " + CsvLoader.ItemSupplementResourceName);
                Services.Log.Error(e.Message);
                throw;
            }
        }

        private ILookup<uint, List<uint>> generateItemsPrerequisites()
        {
            var itemPrerequisiteOptions = new List<(uint ItemId, List<uint> ShopCostIds)>();
            var itemExchangeShops = getRelevantShops(ShopSheet);

            foreach (var shop in itemExchangeShops)
            {
                foreach (var shopItem in shop.Item)
                {
                    var receiveCount = shopItem.ReceiveItems
                        .Min(itemReceive =>
                            itemReceive.ReceiveCount
                        );

                    var itemCosts = shopItem.ItemCosts
                        .Where(itemCost =>
                            itemCost.ItemCost.RowId > MaxCurrencyId
                            && itemCost.ItemCost.Value.ItemUICategory.Value.Name != "Currency"
                            )
                        .SelectMany(itemCost =>
                            Enumerable.Repeat(
                                itemCost.ItemCost.RowId,
                                (int)Math.Min((itemCost.CurrencyCost + receiveCount - 1) / Math.Max(receiveCount, 1), MaxCostQuantity) // round up
                                )
                        ).ToList();

                    // No costed items that are not currency
                    if (itemCosts.Count == 0) continue;

                    foreach (var receiveItem in shopItem.ReceiveItems)
                    {
                        itemPrerequisiteOptions.Add((receiveItem.Item.RowId, itemCosts));
                    }
                }
            }

            return itemPrerequisiteOptions
                .DistinctBy(i => $"{i.ItemId} {string.Join("", i.ShopCostIds)}")
                .ToLookup(i => i.ItemId, i => i.ShopCostIds);
        }
    }
}
