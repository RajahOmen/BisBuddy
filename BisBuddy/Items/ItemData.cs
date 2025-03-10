using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using Dalamud.Game.Inventory;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GearMateria = BisBuddy.Gear.Materia;
using SheetMateria = Lumina.Excel.Sheets.Materia;

namespace BisBuddy.Items
{
    public partial class ItemData
    {
        private ILookup<uint, uint>? itemsCoffers = null;
        private ILookup<uint, List<uint>>? itemsPrerequisites = null;

        public static readonly uint ItemIdHqOffset = 1_000_000;
        public static readonly char HqIcon = '';
        public static readonly char GlamourIcon = '';
        public static readonly int MaxItemPrerequisites = 25;
        private ExcelSheet<Item> ItemSheet { get; init; }
        private ExcelSheet<SpecialShop> ShopSheet { get; init; }
        private ExcelSheet<SheetMateria> Materia { get; init; }
        public ILookup<uint, uint> ItemsCoffers
        {
            get
            {
                if (itemsCoffers == null)
                {
                    itemsCoffers = generateItemsCoffers();
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        itemsCoffers = null;
                    });
                }
                return itemsCoffers;
            }
        }
        public ILookup<uint, List<uint>> ItemsPrerequisites
        {
            get
            {
                if (itemsPrerequisites == null)
                {
                    itemsPrerequisites = generateItemsPrerequisites();
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        itemsPrerequisites = null;
                    });
                }
                return itemsPrerequisites;
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
            Services.Log.Verbose($"Item Prerequisites Found");
            foreach (var item in ItemsPrerequisites)
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
            Services.Log.Verbose($"End Item Prerequisites Found");
#endif
        }

        public uint ConvertItemIdToHq(uint id)
        {
            // return the hq version of the item with the provided id
            // if no hq version exists, return the nq version
            if (!tryGetItemRowById(id, out var item))
                return 0;

            if (!item.CanBeHq)
                return id;

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

        private bool tryGetItemRowById(uint itemId, out Item item)
        {
            if (itemId > ItemIdHqOffset)
                itemId -= ItemIdHqOffset;

            return ItemSheet.TryGetRow(itemId, out item);
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

        public PrerequisiteNode? BuildGearpiecePrerequisiteTree(uint itemId)
        {
            var unitPrerequisiteGroup = buildPrerequisites(itemId);

            if (unitPrerequisiteGroup.GetType() != typeof(PrerequisiteAtomNode))
                throw new Exception($"Item id \"{itemId}\" returned non-unit prereqs group (\"{unitPrerequisiteGroup.GetType().Name}\")");

            // Unit type prerequisite groups should only
            if (unitPrerequisiteGroup.PrerequisiteTree.Count > 1)
                throw new Exception($"Item id \"{itemId}\" returned unit prereqs with \"{unitPrerequisiteGroup.PrerequisiteTree.Count}\" prerequisites");

            // no prereqs to unwrap to, don't want empty unit prereq group for gearpiece
            if (unitPrerequisiteGroup.PrerequisiteTree.Count == 0)
                return null;

            // unwrap highest layer, the geapiece itself acts as the upper unit prereq group
            return unitPrerequisiteGroup.PrerequisiteTree[0];
        }

        private PrerequisiteNode buildPrerequisites(uint itemId, int depth = 8)
        {
            var itemName = GetItemNameById(itemId);
            var group = new PrerequisiteAtomNode(itemId, itemName, [], PrerequisiteNodeSourceType.Item);

            if (depth <= 0)
                return group;

            // build list of prerequisites from supplementals data
            PrerequisiteNode supplementalTree = new PrerequisiteOrNode(itemId, itemName, [], PrerequisiteNodeSourceType.Loot);
            var supplementalPrereqs = ItemsCoffers[itemId];
            var hasSupplementalPrereqs = supplementalPrereqs.Any();

            if (supplementalPrereqs.Count() == 1)
            {
                supplementalTree = buildPrerequisites(supplementalPrereqs.First(), depth - 1);
                supplementalTree.SourceType = PrerequisiteNodeSourceType.Loot;
            }
            else if (supplementalPrereqs.Count() > 1)
            {
                supplementalTree.PrerequisiteTree = ItemsCoffers[itemId]
                    .Select(id => buildPrerequisites(id, depth - 1))
                    .ToList();
            }

            // build list of prerequisites from shops/exchanges data
            PrerequisiteNode exchangesTree = new PrerequisiteOrNode(itemId, itemName, [], PrerequisiteNodeSourceType.Shop);
            var exchangesPrereqs = ItemsPrerequisites[itemId];
            var hasExchangesPrereqs = exchangesPrereqs.Any();

            // only exchangable at one shop listing
            if (exchangesPrereqs.Count() == 1)
            {
                var shopCosts = exchangesPrereqs.First();

                // shop only is requesting one item to exchange
                if (shopCosts.Count == 1)
                {
                    exchangesTree = buildPrerequisites(shopCosts.First(), depth - 1);
                    exchangesTree.SourceType = PrerequisiteNodeSourceType.Shop;
                }
                else // shop is requesting more than one item to exchange
                {
                    var prereqTree = shopCosts
                        .Select(id => buildPrerequisites(id, depth - 1))
                        .ToList();

                    exchangesTree = new PrerequisiteAndNode(
                        itemId,
                        itemName,
                        prereqTree,
                        PrerequisiteNodeSourceType.Shop
                        );
                }

            }
            else if (exchangesPrereqs.Count() > 1) // exchangable at more than 1 shop listing
            {
                // build OR list of ANDs. Ex: OR(AND(A, B, C), AND(D, E), ATOM())
                exchangesTree.PrerequisiteTree = exchangesPrereqs
                    .Select(shopCostIds =>
                    {
                        // shop costs one items
                        if (shopCostIds.Count == 1)
                        {
                            var prereq = buildPrerequisites(shopCostIds.First(), depth - 1);
                            prereq.SourceType = PrerequisiteNodeSourceType.Shop;
                            return prereq;
                        }

                        // Shop costs multiple items
                        return new PrerequisiteAndNode(
                            itemId,
                            itemName,
                            shopCostIds.Select(id => buildPrerequisites(id, depth - 1)).ToList(),
                            PrerequisiteNodeSourceType.Shop
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
                var compoundGroup = new PrerequisiteOrNode(
                    itemId,
                    itemName,
                    [supplementalTree, exchangesTree],
                    PrerequisiteNodeSourceType.Compound
                    );

                group.PrerequisiteTree = [compoundGroup];
            }
            else if (hasSupplementalPrereqs)
            {
                // only from supplementals
                group.PrerequisiteTree = [supplementalTree];
            }
            else if (hasExchangesPrereqs)
            {
                // only from shops/exchanges
                group.PrerequisiteTree = [exchangesTree];
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

        /// <summary>
        /// Use an item's corresponding ClassJobCategory to return the list of job abbreviations that
        /// can equip the item
        /// </summary>
        /// <param name="itemId">The RowId or HQ-Offset RowId for the item</param>
        /// <returns>The set of 3-letter job abbreviations that can equip the item</returns>
        public HashSet<string> GetItemClassJobCategories(uint itemId)
        {
            if (!tryGetItemRowById(itemId, out var itemRow))
                return [];

            var classJobCategory = itemRow.ClassJobCategory.Value;
            var jobs = new HashSet<string>();

            var properties = typeof(ClassJobCategory).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties)
            {
                // only check bool fields
                if (property.PropertyType == typeof(bool))
                {
                    var value = property.GetValue(classJobCategory);
                    if (value != null && (bool)value)
                    {
                        jobs.Add(property.Name);
                    }
                }
            }

            return jobs;
        }

        /// <summary>
        /// Use an item's corresponding EquipSlotCategory to find it's GearpieceType
        /// </summary>
        /// <param name="itemId">The RowId or HQ-Offset RowId for the item</param>
        /// <returns>The corresponding GearpieceType</returns>
        public GearpieceType GetItemGearpieceType(uint itemId)
        {
            if (!tryGetItemRowById(itemId, out var itemRow))
                return GearpieceType.None;

            var equipSlotCategory = itemRow.EquipSlotCategory.Value;

            var properties = typeof(EquipSlotCategory).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties)
            {
                // only check sbyte fields
                if (property.PropertyType == typeof(sbyte))
                {
                    var value = property.GetValue(equipSlotCategory);
                    if (value != null && (sbyte)value == 1)
                    {
                        // return first gearpiece type match
                        if (GearpieceTypeMapper.TryParse(property.Name, out var type))
                            return type;
                    }
                }
            }

            // none found
            return GearpieceType.None;
        }
    }
}
