using BisBuddy.Services.ItemData;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BisBuddy.Items
{
    public partial class ItemDataService
    {
        // to ignore shop costs for common currencies (tomestones, MGP, etc.)
        // doesn't cover currencies added later
        private static readonly int MaxCurrencyId = 99;
        // Max cost item quantity is limited to limit size of graph for assignment
        private static readonly int MaxCostQuantity = 20;

        // map of coffer item.Icon => item.ItemUiCategory.RowId
        private static readonly Dictionary<ushort, List<uint>> CofferIconToEquipSlotCategory = new()
        {
            { 26557, [1, 2, 13] }, // general weapon coffer
            { 26632, [1, 2] },     // PLD arms 1
            { 26633, [1, 2] },     // PLD arms 2
            { 26634, [1, 2] },     // PLD arms 3
            { 26635, [1, 2] },     // PLD arms 4
            { 26558, [3] },        // head
            { 26559, [4] },        // body
            { 26560, [5] },        // gloves
            { 26561, [7] },        // pants
            { 26562, [8] },        // shoes
            // { 26573, [6] }        // belt/waist, ignore
            { 26564, [9] },        // earrings
            { 26565, [10] },       // necklace
            { 26566, [11] },       // bracelet
            { 26567, [12] },       // ring
        };

        private static readonly Regex ItemNameIlvlRegex = new(@"\(IL ([0-9]*)\)");
        private static readonly Regex AugmentedTomestoneGearNameRegex = new(@"\AAugmented");

        private static List<SpecialShop> getRelevantShops(ExcelSheet<SpecialShop> shopSheet)
        {
            var shops = new List<SpecialShop>();

            foreach (var shop in shopSheet) // check all SpecialShops
                shops.Add(shop);

            return shops;
        }

        /// <summary>
        /// Scrape game data to try and find matches for coffers that don't yet exist in LuminaSupplemental due to Eorzea Database not updating
        /// to contain them yet. Adds any it finds to existing ItemSupplement list
        /// </summary>
        /// <param name="existingCofferSources">List of existing loot supplement relations</param>
        /// <param name="itemSheet">The game itemSheet</param>
        private static void addNewCoffers(List<(ItemSupplement Item, CofferSourceType SourceType)> existingCofferSources, ExcelSheet<Item> itemSheet)
        {
            // list of ids of coffer-like items with known contents
            var knownCoffers = existingCofferSources
                .Select(entry => entry.Item.SourceItemId)
                .ToHashSet();

            // get list of coffer-like items with unknown contents
            var unknownCoffers = new HashSet<Item>();
            var unknownCofferIlvls = new HashSet<uint>();
            var candidateItems = new Dictionary<uint, List<Item>>();
            var candidateCoffers = new Dictionary<uint, List<Item>>();
            foreach (var item in itemSheet.Where(item => !knownCoffers.Contains(item.RowId)))
            {
                var itemName = item.Name.ToDalamudString().TextValue;
                if (CofferIconToEquipSlotCategory.ContainsKey(item.Icon))
                {
                    var match = ItemNameIlvlRegex.Match(itemName);
                    if (!match.Success)
                        continue;

                    var itemLevel = uint.Parse(match.Groups[1].Value);
                    unknownCoffers.Add(item);
                    unknownCofferIlvls.Add(itemLevel);
                    candidateItems[itemLevel] = [];
                    if (!candidateCoffers.TryGetValue(itemLevel, out var coffersAtIlvl))
                        candidateCoffers[itemLevel] = [item];
                    else
                        coffersAtIlvl.Add(item);
                }
            }

            // no coffers with unknown contents
            if (unknownCoffers.Count == 0)
                return;

            // retrieve items that may feasibly come from unknown coffers
            foreach (var item in itemSheet)
            {
                // a coffer, ignore
                if (unknownCoffers.Contains(item) || knownCoffers.Contains(item.RowId))
                    continue;

                // cannot be equipped, not a gearpiece
                if (item.EquipSlotCategory.RowId == 0)
                    continue;

                // this is a augmented tomestone gearpiece, cannot be from coffer
                var itemName = item.Name.ToDalamudString().TextValue;
                if (AugmentedTomestoneGearNameRegex.IsMatch(itemName))
                    continue;

                // potential candidate item
                if (unknownCofferIlvls.Contains(item.LevelItem.RowId))
                    candidateItems[item.LevelItem.RowId].Add(item);
            }

            // solve per relevant ilvl
            foreach (var ilvl in unknownCofferIlvls)
            {
                var coffersAtIlvl = candidateCoffers[ilvl];
                var itemsAtIlvl = candidateItems[ilvl];
                // group items by EquipSlotCategory/ClassJobCategory pair, since cannot contain multiple from each grouping from 1 coffer
                var groupedItemsAtIlvl = itemsAtIlvl
                    .GroupBy(
                        item => new
                        {
                            equipSlotCategory = item.EquipSlotCategory.RowId,
                            classJobCategory = item.ClassJobCategory.RowId,
                        });

                foreach (var coffer in coffersAtIlvl)
                {
                    var cofferName = coffer.Name.ToDalamudString().TextValue;
                    var cofferNameLev = new Fastenshtein.Levenshtein(cofferName);
                    var cofferEquipSlot = CofferIconToEquipSlotCategory.GetValueOrDefault(coffer.Icon) ?? [];

                    foreach (var itemGroup in groupedItemsAtIlvl.Where(g => cofferEquipSlot.Contains(g.Key.equipSlotCategory)))
                    {
                        // get item whos name most closely matches name of coffer
                        var bestMatch = itemGroup
                            .OrderBy(item => cofferNameLev.DistanceFrom(item.Name.ToDalamudString().TextValue))
                            .First();

                        var newItemSupplement = new ItemSupplement(bestMatch.RowId, coffer.RowId, ItemSupplementSource.Loot);
                        existingCofferSources.Add((newItemSupplement, CofferSourceType.ClosestMatch));
                    }
                }
            }
        }

        private ILookup<uint, (uint, CofferSourceType)> generateItemsCoffers(ExcelSheet<Item> itemSheet)
        {
            try
            {
                var lines = CsvLoader.LoadResource<ItemSupplement>(
                    CsvLoader.ItemSupplementResourceName,
                    true,
                    out var failedLines,
                    out var exceptions,
                    dataManager.GameData,
                    dataManager.GameData.Options.DefaultExcelLanguage
                    );

                if (failedLines.Count != 0)
                {
                    foreach (var failedLine in failedLines)
                    {
                        logger.Error("Failed to load line from " + CsvLoader.ItemSupplementResourceName + ": " + failedLine);
                    }
                }

                var cofferLines = lines
                    .Where(item => item.ItemSupplementSource == ItemSupplementSource.Loot)
                    .Select(item => (Item: item, SourceType: CofferSourceType.LuminaSupplemental))
                    .ToList();

                // use game data to try and parse new coffers that don't exist in pulled data
                addNewCoffers(cofferLines, itemSheet);

                return cofferLines
                    .ToLookup(i => i.Item.ItemId, i => (i.Item.SourceItemId, i.SourceType));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to generate coffers using " + CsvLoader.ItemSupplementResourceName);
                throw;
            }
        }

        private ILookup<uint, (List<uint> ItemIds, uint SourceShopId)> generateItemsPrerequisites()
        {
            var itemPrerequisiteOptions = new List<(uint ItemId, List<uint> ShopCostIds, uint SourceShopId)>();
            var itemExchangeShops = getRelevantShops(ShopSheet);

            foreach (var shop in itemExchangeShops)
            {
                foreach (var shopItem in shop.Item)
                {
                    var receiveItems = shopItem.ReceiveItems.Where(item => item.Item.RowId != 0);

                    // not actually recieving an item
                    if (!receiveItems.Any())
                        continue;

                    var receiveCount = receiveItems
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
                    if (itemCosts.Count == 0)
                        continue;

                    foreach (var receiveItem in receiveItems)
                        itemPrerequisiteOptions.Add((receiveItem.Item.RowId, itemCosts, shop.RowId));
                }
            }

            return itemPrerequisiteOptions
                .DistinctBy(i => $"{i.ItemId}|{i.SourceShopId}|{string.Join("", i.ShopCostIds)}")
                .ToLookup(i => i.ItemId, i => (i.ShopCostIds, i.SourceShopId));
        }
    }
}
