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
    public partial class ItemData
    {
        // to ignore shop costs for common currencies (tomestones, MGP, etc.)
        // doesn't cover currencies added later
        private static readonly int MaxCurrencyId = 99;
        // Max cost item quantity is limited to limit size of graph for assignment
        private static readonly int MaxCostQuantity = 20;

        // RAID SHOP REGEX
        // regex to match items that can be traded for raid gear
        private static readonly Regex RaidBookRegex = new(@"can be traded for special gear");
        private static readonly string TotemShopString = "Totem Gear";
        private static readonly string IdolShopString = "Idol Gear"; // SHB memoria misera
        private static readonly string DiscipleShopString = @"\((DoW|DoM)\)";
        private static readonly Regex ShopNameRegex = new($"({TotemShopString}|{IdolShopString}|{DiscipleShopString})");

        // COFFER REGEX
        // When want to enforce a match between coffer name and gear name
        // ex: *Clouddark* Armor of Fending -> *Clouddark* Chest Gear Coffer, NOT other coffer without Clouddark in name
        private static readonly string ItemCofferMatchStrings = @"\b(Clouddark|)";
        // regex in coffer name that indicate what slot the coffer will give gear for
        private static readonly string GearCofferSlotNames = @"\b(Weapon|Head|Genji Kabuto|Chest|Genji Armor|Hand|Genji Kote|Leg|Genji Tsutsu-hakama|Foot|Genji Sune-ate|Earring|Necklace|Bracelet|Ring)";
        // regex in coffer name that indicates the ilvl the coffer will give gear of
        private static readonly string GearCofferIlvl = @"Coffer \(IL (\d+)\)";
        private static readonly Regex GearCofferRegex = new(
            @$"{GearCofferSlotNames}.*{GearCofferIlvl}",
            RegexOptions.IgnoreCase
        );

        // AUGMENT SHOP REGEX
        private static readonly Regex ItemMatchRegex = new($"{ItemCofferMatchStrings}");
        private static readonly string[] NoCofferExclusionStrings =
            [
                "Augmented", // upgraded tome gear
                "Ultimate",  // ultimate raids
                "Exquisite", // criterion savage upgraded tome gear
                "Edencall",  // edge case (memoria misera): to avoid assigning to idealized coffers
            ];
        private static readonly Regex NoCofferRegex = new($"({string.Join("|", NoCofferExclusionStrings)})");

        // for standard savage raid shops
        private static readonly Regex SavageRaidUpgradeMatRegex = new(@"\b(weapons|vestments|accessories) enables full optimization of the gear's (offensive|defensive) properties");
        // for normal raid shops
        private static readonly Regex NormalRaidUpgradeMatRegex = new(@"(One|Two|Four).*special \b(headgear|body gear|arm gear|leg gear|foot gear|accessory)\.");
        // for criterion shops that augment augmented weapons using criterion savage drops
        private static readonly Regex CriterionUpgradeMatRegex = new(@"previously augmented (weaponry)");

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
