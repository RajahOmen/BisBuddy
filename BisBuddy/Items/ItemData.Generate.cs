using BisBuddy.Gear;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BisBuddy.Items
{
    public partial class ItemData
    {
        // RAID SHOP REGEX
        // regex to match items that can be traded for raid gear
        private static readonly Regex RaidBookRegex = new(@"can be traded for special gear");

        // COFFER REGEX
        // When want to enforce a match between coffer name and gear name
        // ex: *Clouddark* Armor of Fending -> *Clouddark* Chest Gear Coffer, NOT other coffer without Clouddark in name
        private static readonly string ItemCofferMatchStrings = @"\b(Clouddark|)";
        // regex in coffer name that indicate what slot the coffer will give gear for
        private static readonly string GearCofferSlotNames = @"\b(Weapon|Head|Genji Kabuto|Chest|Genji Armor|Hand|Genji Kote|Leg|Genji Tsutsu-hakama|Foot|Genji Sune-ate|Earring|Necklace|Bracelet|Ring)";
        // regex in coffer name that indicates the ilvl the coffer will give gear of
        private static readonly string GearCofferIlvl = @"Coffer \(IL (\d+)\)";
        private static readonly Regex GearCofferRegex = new(
            $"{ItemCofferMatchStrings}.*{GearCofferSlotNames}.*{GearCofferIlvl}"
        );

        // AUGMENT SHOP REGEX
        private static readonly Regex ItemMatchRegex = new($"{ItemCofferMatchStrings}");
        // for standard savage raid shops
        private static readonly Regex SavageRaidUpgradeMatRegex = new(@"\b(weapons|vestments|accessories) enables full optimization of the gear's (offensive|defensive) properties");
        // for normal raid shops
        private static readonly Regex NormalRaidUpgradeMatRegex = new(@"(One|Two|Four).*special \b(headgear|body gear|arm gear|leg gear|foot gear|accessory)\.");
        // for criterion shops that augment augmented weapons using criterion savage drops
        private static readonly Regex CriterionUpgradeMatRegex = new(@"previously augmented (weaponry)");

        private static List<(uint id, GearpieceType type, uint ilvl, string matchString, string name)> getCofferInfo(ExcelSheet<Item> itemSheet)
        {
            // coffer id, slot type (e.g. body, head, etc.), ilvl, match string (optional)
            var cofferInfo = new List<(uint id, GearpieceType type, uint ilvl, string matchString, string name)>();

            foreach (var item in itemSheet)
            {
                var cofferMatch = GearCofferRegex.Match(item.Name.ToString());
                if (
                    cofferMatch.Success
                    && GearpieceTypeMapper.TryParse(cofferMatch.Groups[2].Value.Replace("-", null).Replace("Genji ", null), out var cofferType) // matches a gearpiece
                    && uint.TryParse(cofferMatch.Groups[3].Value, out var cofferIlvl) // parsed ilvl
                    && (
                        (cofferType == GearpieceType.Weapon && cofferIlvl % 10 == 5)
                        || cofferType != GearpieceType.Weapon
                        ) // if item is a weapon coffer, ilvl must end in 5
                    ) // item is a gear coffer
                {
                    cofferInfo.Add((item.RowId, cofferType, cofferIlvl, cofferMatch.Groups[1].Value, item.Name.ToString()));
                }
            }
            return cofferInfo;
        }

        private static (List<SpecialShop>, List<SpecialShop>) getRelevantShops(ExcelSheet<SpecialShop> shopSheet)
        {
            var raidBookShops = new List<SpecialShop>();
            var itemExchangeShops = new List<SpecialShop>();

            foreach (var shop in shopSheet) // check all SpecialShops
            {
                var shopAdded = false;
                foreach (var shopItem in shop.Item) // check all items in this shop
                {
                    foreach (var costItem in shopItem.ItemCosts) // look through what this item costs
                    {
                        if (RaidBookRegex.IsMatch(costItem.ItemCost.Value.Description.ToString())) // costs a raid book
                        {
                            raidBookShops.Add(shop);
                            shopAdded = true;
                            break;
                        }
                        if (
                            SavageRaidUpgradeMatRegex.IsMatch(costItem.ItemCost.Value.Description.ToString())
                            || NormalRaidUpgradeMatRegex.IsMatch(costItem.ItemCost.Value.Description.ToString())
                            || CriterionUpgradeMatRegex.IsMatch(costItem.ItemCost.Value.Description.ToString())
                            ) // costs an upgrade material we care about
                        {
                            itemExchangeShops.Add(shop);
                            shopAdded = true;
                            break;
                        }
                    }
                    if (shopAdded) break; // don't check more items, already added to a shop
                }
            }

            return (raidBookShops, itemExchangeShops);
        }

        private static Dictionary<uint, uint> generateItemsCoffers(
            List<SpecialShop> raidBookShops,
            List<(uint id, GearpieceType type, uint ilvl, string matchString, string name)> cofferInfo
            )
        {
            var itemsCoffers = new Dictionary<uint, uint>();
            foreach (var shop in raidBookShops)
            {
                foreach (var shopItem in shop.Item)
                {
                    foreach (var receiveItem in shopItem.ReceiveItems)
                    {
                        var itemInfo = receiveItem.Item.Value;

                        // get the slot this item equips to
                        var gearpieceType = getGearpieceType(itemInfo);

                        if (
                            itemInfo.ClassJobCategory.RowId != 0 // equipped by a job/jobs
                            && gearpieceType != null // item is equipped in a slot on your character
                            && cofferInfo.FirstOrDefault(k => k.type == gearpieceType && k.ilvl == itemInfo.LevelItem.RowId) is var matchingCoffer && matchingCoffer != default // coffer matching ilvl and slot exists
                            ) // valid item that can be recieved from a coffer
                        {
                            var itemMatch = ItemMatchRegex.Match(SeString.Parse(itemInfo.Name).TextValue);

                            // item match string doesn't match coffer match string
                            if (itemMatch.Success && itemMatch.Groups[1].Value != matchingCoffer.matchString) continue;

                            itemsCoffers[itemInfo.RowId] = matchingCoffer.id;
                        }
                    }
                }
            }

            return itemsCoffers;
        }

        private static Dictionary<uint, List<uint>> generateItemsPrerequesites(List<SpecialShop> itemExchangeShops)
        {
            var itemsPrerequesites = new Dictionary<uint, List<uint>>();
            var normalRaidShopQuantities = new Dictionary<string, int>()
            {
                { "One", 1 },
                { "Two", 2 },
                { "Four", 4 },
            };

            foreach (var shop in itemExchangeShops)
            {
                foreach (var shopItem in shop.Item)
                {
                    // for shop types that only have one exchange item
                    var upgradeMatOnlyOverride = false;
                    // for shops that require more than one item to exchange
                    var upgradeMatQuantity = 1;
                    uint? upgradeMatId = null;
                    uint? unupgradedItemId1 = null;
                    uint? unupgradedItemId2 = null; // for paladin shields
                    foreach (var costItem in shopItem.ItemCosts) // get the id of the upgrade material used to upgrade the recieved item
                    {
                        var matchSavage = SavageRaidUpgradeMatRegex.Match(costItem.ItemCost.Value.Description.ToString());
                        if (
                            matchSavage.Success
                            && GearpieceTypeMapper.TryParse(matchSavage.Groups[1].Value, out _)
                            )
                        {
                            upgradeMatId = costItem.ItemCost.RowId;
                            continue; // found the upgrade material, this isnt gear
                        }

                        var matchNormal = NormalRaidUpgradeMatRegex.Match(costItem.ItemCost.Value.Description.ToString());
                        if (
                            matchNormal.Success
                            && GearpieceTypeMapper.TryParse(matchNormal.Groups[2].Value, out _)
                            )
                        {
                            upgradeMatQuantity = normalRaidShopQuantities[matchNormal.Groups[1].Value];
                            upgradeMatOnlyOverride = true; // dont have to exchange gear, only upgrade mat
                            upgradeMatId = costItem.ItemCost.RowId;
                            continue; // found the upgrade material, this isnt gear
                        }

                        var matchCriterion = CriterionUpgradeMatRegex.Match(costItem.ItemCost.Value.Description.ToString());
                        if (
                            matchCriterion.Success
                            && GearpieceTypeMapper.TryParse(matchCriterion.Groups[1].Value, out _)
                            )
                        {
                            upgradeMatId = costItem.ItemCost.RowId;
                            continue; // found the upgrade material, this isnt gear
                        }

                        var itemInfo = costItem.ItemCost.Value;
                        var gearpieceType = getGearpieceType(itemInfo);

                        if (
                            itemInfo.ClassJobCategory.RowId != 0 // equipped by a job/jobs
                            && gearpieceType != null // item is equipped in a slot on your character
                            )
                        {
                            if (unupgradedItemId1 == null) unupgradedItemId1 = itemInfo.RowId;
                            else unupgradedItemId2 = itemInfo.RowId;
                        }
                    }

                    if (upgradeMatId == null || (unupgradedItemId1 == null && !upgradeMatOnlyOverride)) // no upgrade item found
                    {
                        continue;
                    }

                    foreach (var receiveItem in shopItem.ReceiveItems)
                    {
                        var itemInfo = receiveItem.Item.Value;

                        // get the slot this item equips to
                        var gearpieceType = getGearpieceType(itemInfo);

                        if (
                            itemInfo.ClassJobCategory.RowId != 0 // equipped by a job/jobs
                            && gearpieceType != null // item is equipped in a slot on your character
                            )
                        {
                            if (unupgradedItemId1 != null) // assign first unupgraded item
                            {
                                var prereqs = new List<uint>();
                                if (unupgradedItemId1 != null) prereqs.Add(unupgradedItemId1.Value);
                                if (upgradeMatId != null) prereqs.AddRange(Enumerable.Repeat(upgradeMatId.Value, upgradeMatQuantity));
                                itemsPrerequesites[itemInfo.RowId] = prereqs;
                                unupgradedItemId1 = null;
                            }
                            else // if first unupgraded item is already assigned, assign second (only for paladin shields)
                            {
                                var prereqs = new List<uint>();
                                if (unupgradedItemId2 != null) prereqs.Add(unupgradedItemId2.Value);
                                if (upgradeMatId != null) prereqs.AddRange(Enumerable.Repeat(upgradeMatId.Value, upgradeMatQuantity));
                                itemsPrerequesites[itemInfo.RowId] = prereqs;
                            }
                        }
                    }
                }
            }

            return itemsPrerequesites;
        }

        private static (Dictionary<uint, uint>, Dictionary<uint, List<uint>>)
            generateItemRelations(ExcelSheet<Item> itemSheet, ExcelSheet<SpecialShop> shopSheet)
        {
            // coffer id, slot type (e.g. body, head, etc.), ilvl
            var cofferInfo = getCofferInfo(itemSheet);

            var (raidBookShops, itemExchangeShops) = getRelevantShops(shopSheet);

            // item id -> coffer id that the item is obtained from
            var itemsCoffers = generateItemsCoffers(raidBookShops, cofferInfo);

            // item id -> upgrade material id that the item can be upgraded fromd
            var itemsPrerequesites = generateItemsPrerequesites(itemExchangeShops);

#if DEBUG
            // minimum item id to display for debug logging. Update with new patches to review generations
            // filters out most older items for easier debugging
            uint debugMinItemId = 44500; // lower than most recent to ensure get new items 

            Services.Log.Verbose("Coffer Relations Found");
            foreach (var item in itemsCoffers)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var itemName = itemSheet.GetRow(item.Key).Name.ToString();
                var cofferName = itemSheet.GetRow(item.Value).Name.ToString();
                Services.Log.Verbose($"{cofferName,-50} => {itemName}");
            }
            Services.Log.Verbose("End Coffer Relations Found");
            Services.Log.Verbose($"Item Prerequesites Found");
            foreach (var item in itemsPrerequesites)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var recieveItemName = itemSheet.GetRow(item.Key).Name.ToString();
                var prereqItemNames = item.Value.Select(i => itemSheet.GetRow(i).Name.ToString());
                Services.Log.Verbose($"{string.Join(" + ", prereqItemNames),-60} => {recieveItemName}");
            }
            Services.Log.Verbose($"End Item Prerequesites Found");
#endif

            return (itemsCoffers, itemsPrerequesites);
        }
    }
}
