using BisBuddy.Gear.Prerequesites;
using Dalamud.Game.Inventory;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using SheetMateria = Lumina.Excel.Sheets.Materia;
using GearMateria = BisBuddy.Gear.Materia;
using System.Threading.Tasks;

namespace BisBuddy.Items
{
    public partial class ItemData
    {
        private ILookup<uint, uint>? itemsCoffers = null;
        private ILookup<uint, List<uint>>? itemsPrerequesites = null;

        public static readonly uint ItemIdHqOffset = 1_000_000;
        public static readonly char HqIcon = '';
        public static readonly char GlamourIcon = '';
        public static readonly int MaxItemPrerequesites = 25;
        private ExcelSheet<Item> ItemSheet { get; init; }
        private ExcelSheet<SpecialShop> ShopSheet { get; init; }
        private ExcelSheet<SheetMateria> Materia { get; init; }
        public ILookup<uint, uint> ItemsCoffers {
            get
            {
                if (itemsCoffers == null)
                {
                    itemsCoffers = generateItemsCoffers();
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        itemsCoffers = null;
                    });
                }
                return itemsCoffers;
            }
        }
        public ILookup<uint, List<uint>> ItemsPrerequesites {
            get
            {
                if (itemsPrerequesites == null)
                {
                    itemsPrerequesites = generateItemsPrerequesites();
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        itemsPrerequesites = null;
                    });
                }
                return itemsPrerequesites;
            }
        }
        private Dictionary<string, uint> NameToId { get; init; }
        private Dictionary<string, (string statName, int statLevel, int statQuantity)> MateriaNameToStat { get; init; } = [];
        private Dictionary<(uint materiaId, int materiaGrade), uint> materiaItemIds { get; init; }

        public ItemData(ExcelModule luminaExcelModule)
        {
            ItemSheet = luminaExcelModule.GetSheet<Item>() ?? throw new ArgumentException("Item sheet not found");
            ShopSheet = luminaExcelModule.GetSheet<SpecialShop>() ?? throw new InvalidOperationException("Special shop sheet not found");
            Materia = luminaExcelModule.GetSheet<SheetMateria>() ?? throw new InvalidOperationException("Materia sheet not found");
            NameToId = [];
            materiaItemIds = [];
#if DEBUG
            // minimum item id to display for debug logging. Update with new patches to review generations
            // filters out most older items for easier debugging
            uint debugMinItemId = 44500; // lower than most recent to ensure get new items 

            Services.Log.Verbose("Coffer Relations Found");
            foreach (var item in ItemsCoffers)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var itemName = ItemSheet.GetRow(item.Key).Name.ToString();
                foreach (var cofferId in item)
                {
                    var cofferName = ItemSheet.GetRow(cofferId).Name.ToString();
                    ;
                    Services.Log.Verbose($"{cofferName,-50} => {itemName}");
                }
            }
            Services.Log.Verbose("End Coffer Relations Found");
            Services.Log.Verbose($"Item Prerequesites Found");
            foreach (var item in ItemsPrerequesites)
            {
                // only show "new" items
                if (item.Key < debugMinItemId) continue;

                var recieveItemName = ItemSheet.GetRow(item.Key).Name.ToString();
                foreach (var itemId in item)
                {
                    var prereqItemNames = itemId.Select(id => ItemSheet.GetRow(id).Name.ToString());
                    Services.Log.Verbose($"{string.Join(" + ", prereqItemNames.GroupBy(n => n).Select(g => $"{g.Count()}x {g.Key}")),-60} => {recieveItemName}");
                }
            }
            Services.Log.Verbose($"End Item Prerequesites Found");
#endif
        }

        public uint ConvertItemIdToHq(uint id)
        {
            // return the hq version of the item with the provided id
            // if no hq version exists, return the nq version
            var item = ItemSheet.GetRow(id);

            if (!item.CanBeHq) return id;

            return id + ItemIdHqOffset;
        }

        public string GetItemNameById(uint id)
        {
            // check if item is HQ, change Id to NQ if it is
            var modifiedId = id;
            var itemIsHq = id >= ItemIdHqOffset;
            if (itemIsHq) modifiedId -= ItemIdHqOffset;

            // returns the name of the item with the provided id
            var itemName = ItemSheet.GetRow(modifiedId).Name.ToString();

            // add Hq icon to the item name if it is hq
            if (itemIsHq) itemName = $"{itemName} {HqIcon}";

            NameToId[itemName] = id;
            return itemName;
        }

        public uint GetItemIdByName(string name)
        {
            // return cached value
            if (NameToId.TryGetValue(name, out var value)) return value;

            var itemIsHq = name.Contains(HqIcon);
            if (itemIsHq) name = name[..^2]; // remove hq icon (remove hq icon and space)

            // get from item sheet if not cached
            var id = ItemSheet.FirstOrDefault(item => item.Name.ToString() == name).RowId;

            // convert to HQ id if item is HQ
            if (itemIsHq) id += ItemIdHqOffset;

            NameToId[name] = id;
            return id;
        }

        public List<uint> GetItemMateriaIds(GameInventoryItem item)
        {
            // returns a list of materia ids that are melded to item
            var materiaList = new List<uint>();

            // iterate over materia slots
            for (var i = 0; i < item.Materia.Length; i++)
            {
                var materiaId = item.Materia[i];
                var materiaGrade = item.MateriaGrade[i];

                // no materia in this slot, assume all after are empty
                if (materiaId == 0) break;

                // add item id of materia to list
                try
                {
                    var materiaItemId = GetMateriaItemId(materiaId, materiaGrade);
                    if (materiaItemId != 0) materiaList.Add(materiaItemId);
                }
                catch (Exception e)
                {
                    Services.Log.Error(e, $"Failed to get materia item id for item {GetItemNameById(item.ItemId)} (materia id {materiaId}, materia grade {materiaGrade})");
                    DumpData(item);
                }
            }

            return materiaList;
        }

        private unsafe void DumpData(GameInventoryItem item)
        {
            var data = new byte[0x48];
            var ptr = (byte*)item.Address;
            for (var i = 0; i < 0x48; i++)
            {
                data[i] = ptr[i];
            }
            var str = string.Join(' ', data.Select(t => t.ToString("X")));
            Services.Log.Fatal(str);
        }

        public List<GearMateria> GetItemMateria(GameInventoryItem item)
        {
            var materiaIds = GetItemMateriaIds(item);
            var materiaList = new List<GearMateria>();
            foreach (var id in materiaIds)
            {
                materiaList.Add(BuildMateria(id));
            }

            return materiaList;
        }

        private uint GetMateriaItemId(ushort materiaId, byte materiaGrade)
        {
            // can fail for weird gear, like Eternal Ring//
            if (!Materia.TryGetRow(materiaId, out var materiaRow)) return 0;

            // row is materiaId (the type: crt, det, etc), column is materia grade (I, II, III, etc)
            var materiaItem = materiaRow.Item[materiaGrade];

            return materiaItem.RowId;
        }

        public PrerequesiteNode? BuildGearpiecePrerequesiteGroup(uint itemId)
        {
            var unitPrerequesiteGroup = BuildPrerequesites(itemId);

            if (unitPrerequesiteGroup.GetType() != typeof(PrerequesiteAtomNode))
                throw new Exception($"Item id \"{itemId}\" returned non-unit prereqs group (\"{unitPrerequesiteGroup.GetType().Name}\")");

            // Unit type prerequesite groups should only
            if (unitPrerequesiteGroup.PrerequesiteTree.Count > 1)
                throw new Exception($"Item id \"{itemId}\" returned unit prereqs with \"{unitPrerequesiteGroup.PrerequesiteTree.Count}\" prerequesites");

            // no prereqs to unwrap to, don't want empty unit prereq group for gearpiece
            if (unitPrerequesiteGroup.PrerequesiteTree.Count == 0)
                return null;

            // unwrap highest layer, the geapiece itself acts as the upper unit prereq group
            return unitPrerequesiteGroup.PrerequesiteTree[0];
        }

        private PrerequesiteNode BuildPrerequesites(uint itemId, int depth = 8)
        {
            var itemName = GetItemNameById(itemId);
            var group = new PrerequesiteAtomNode(itemId, itemName, [], PrerequesiteNodeSourceType.Item);

            if (depth <= 0)
                return group;

            // build list of prerequesites from supplementals data
            PrerequesiteNode supplementalTree = new PrerequesiteOrNode(itemId, itemName, [], PrerequesiteNodeSourceType.Loot);
            var supplementalPrereqs = ItemsCoffers[itemId];
            var hasSupplementalPrereqs = supplementalPrereqs.Any();

            if (supplementalPrereqs.Count() == 1)
            {
                supplementalTree = BuildPrerequesites(supplementalPrereqs.First(), depth - 1);
                supplementalTree.SourceType = PrerequesiteNodeSourceType.Loot;
            } else if (supplementalPrereqs.Count() > 1)
            {
                supplementalTree.PrerequesiteTree = ItemsCoffers[itemId]
                    .Select(id => BuildPrerequesites(id, depth - 1))
                    .ToList();
            }

            // build list of prerequesites from shops/exchanges data
            PrerequesiteNode exchangesTree = new PrerequesiteOrNode(itemId, itemName, [], PrerequesiteNodeSourceType.Shop);
            var exchangesPrereqs = ItemsPrerequesites[itemId];
            var hasExchangesPrereqs = exchangesPrereqs.Any();

            // only exchangable at one shop listing
            if (exchangesPrereqs.Count() == 1)
            {
                var shopCosts = exchangesPrereqs.First();

                // shop only is requesting one item to exchange
                if (shopCosts.Count == 1)
                {
                    exchangesTree = BuildPrerequesites(shopCosts.First(), depth - 1);
                    exchangesTree.SourceType = PrerequesiteNodeSourceType.Shop;
                } else // shop is requesting more than one item to exchange
                {
                    var prereqTree = shopCosts
                        .Select(id => BuildPrerequesites(id, depth - 1))
                        .ToList();

                    exchangesTree = new PrerequesiteAndNode(
                        itemId,
                        itemName,
                        prereqTree,
                        PrerequesiteNodeSourceType.Shop
                        );
                }
                
            } else if (exchangesPrereqs.Count() > 1) // exchangable at more than 1 shop listing
            {
                // build OR list of ANDs. Ex: OR(AND(A, B, C), AND(D, E), ATOM())
                exchangesTree.PrerequesiteTree = exchangesPrereqs
                    .Select(shopCostIds => {
                        // shop costs one items
                        if (shopCostIds.Count == 1)
                        {
                            var prereq = BuildPrerequesites(shopCostIds.First(), depth - 1);
                            prereq.SourceType = PrerequesiteNodeSourceType.Shop;
                            return prereq;
                        }

                        // Shop costs multiple items
                        return new PrerequesiteAndNode(
                            itemId,
                            itemName,
                            shopCostIds.Select(id => BuildPrerequesites(id, depth - 1)).ToList(),
                            PrerequesiteNodeSourceType.Shop
                            );
                    }).ToList();
            }

            // build resulting group
            if (
                hasSupplementalPrereqs
                && hasExchangesPrereqs
                )
            {
                // composed of both shop/exchange sources and supplemental sources
                var compoundGroup = new PrerequesiteOrNode(
                    itemId,
                    itemName,
                    [supplementalTree, exchangesTree],
                    PrerequesiteNodeSourceType.Compound
                    );

                group.PrerequesiteTree = [ compoundGroup ];
            } else if (hasSupplementalPrereqs)
            {
                // only from supplementals
                group.PrerequesiteTree = [ supplementalTree ]; 
            } else if (hasExchangesPrereqs)
            {   
                // only from shops/exchanges
                group.PrerequesiteTree = [ exchangesTree ];
            }

            return group;
        }

        public static List<GameInventoryItem> GetGameInventoryItems(GameInventoryType[] sources)
        {
            var itemsList = new List<GameInventoryItem>();
            foreach (var source in sources)
            {
                var items = Services.GameInventory.GetInventoryItems(source);

                foreach (var item in items)
                {
                    if (item.ItemId == 0) continue;
                    for (var i = 0; i < item.Quantity; i++)
                    {
                        // add each item in stack individually
                        itemsList.Add(item);
                    }
                }
            }
            return itemsList;
        }

        public static uint GameInventoryItemId(GameInventoryItem item)
        {
            // 'normal' item
            if (!item.IsHq) return item.ItemId;

            // hq item
            return item.ItemId + ItemIdHqOffset;
        }

        private (string statName, int statLevel, int statQuantity) getMateriaInfo(string materiaName)
        {
            if (MateriaNameToStat.TryGetValue(materiaName, out var value)) return value;

            var maxMateriaRow = 40;
            SheetMateria? materiaRow = null;
            var materiaCol = -1;

            for (var i = 0; i < maxMateriaRow; i++)
            {
                var row = Materia.GetRowAt(i);
                for (var j = 0; j < 16; j++)
                {
                    var col = row.Item[j];
                    if (col.Value.Name.ExtractText() == materiaName)
                    {
                        materiaRow = row;
                        materiaCol = j;
                        break;
                    }
                }
                if (materiaCol > 0) break;
            }

            if (materiaRow == null || materiaCol < 0)
                throw new InvalidOperationException($"Materia {materiaName} not found in materia sheet");

            var statName = materiaRow.Value.BaseParam.Value.Name.ExtractText();
            var statQuantity = materiaRow.Value.Value[materiaCol];

            return (statName, materiaCol, statQuantity);
        }

        public GearMateria BuildMateria(uint itemId)
        {
            var materiaName = GetItemNameById(itemId);
            var (statName, statLevel, statQuantity) = getMateriaInfo(materiaName);

            return new GearMateria(
                itemId,
                materiaName,
                statLevel,
                statName,
                statQuantity,
                false
                );
        }

        public bool ItemIsShield(uint itemId)
        {
            if (!ItemSheet.TryGetRow(itemId, out var itemRow)) return false;

            // item is equippable to only the shield slot
            return itemRow.EquipSlotCategory.RowId == 2u;
        }
    }
}
