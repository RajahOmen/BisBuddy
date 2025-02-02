using BisBuddy.Gear;
using Dalamud.Game.Inventory;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Items
{
    public partial class ItemData
    {
        public static readonly uint ItemIdHqOffset = 1_000_000;
        public static readonly char HqIcon = '';
        public static readonly char GlamourIcon = '';
        public static readonly int MaxItemPrerequesites = 25;
        private ExcelSheet<Item> ItemSheet { get; init; }
        private ExcelSheet<SpecialShop> ShopSheet { get; init; }
        private ExcelSheet<Lumina.Excel.Sheets.Materia> Materia { get; init; }
        public Dictionary<uint, uint> ItemsCoffers { get; init; }
        private Dictionary<string, uint> NameToId { get; init; }
        private Dictionary<string, (string statName, int statLevel, int statQuantity)> MateriaNameToStat { get; init; } = [];
        private Dictionary<(uint materiaId, int materiaGrade), uint> materiaItemIds { get; init; }
        public Dictionary<uint, List<uint>> ItemPrerequesites { get; init; }

        public ItemData(ExcelModule luminaExcelModule)
        {
            ItemSheet = luminaExcelModule.GetSheet<Item>() ?? throw new ArgumentException("Item sheet not found");
            ShopSheet = luminaExcelModule.GetSheet<SpecialShop>() ?? throw new InvalidOperationException("Special shop sheet not found");
            Materia = luminaExcelModule.GetSheet<Lumina.Excel.Sheets.Materia>() ?? throw new InvalidOperationException("Materia sheet not found");

            var (itemsCoffers, itemsAugments) = generateItemRelations(ItemSheet, ShopSheet); // initialize ItemsCoffers and ItemsAugments
            ItemsCoffers = itemsCoffers;
            ItemPrerequesites = itemsAugments;
            NameToId = [];
            materiaItemIds = [];
        }

        private static GearpieceType? getGearpieceType(Item item)
        {
            var slots = item.EquipSlotCategory.Value;

            if (slots.MainHand == 1) return GearpieceType.Weapon;
            if (slots.OffHand == 1) return GearpieceType.OffHand;
            if (slots.Head == 1) return GearpieceType.Head;
            if (slots.Body == 1) return GearpieceType.Body;
            if (slots.Gloves == 1) return GearpieceType.Hands;
            if (slots.Legs == 1) return GearpieceType.Legs;
            if (slots.Feet == 1) return GearpieceType.Feet;
            if (slots.Ears == 1) return GearpieceType.Ears;
            if (slots.Neck == 1) return GearpieceType.Neck;
            if (slots.Wrists == 1) return GearpieceType.Wrists;
            if (slots.FingerL == 1) return GearpieceType.Finger;
            if (slots.FingerR == 1) return GearpieceType.Finger;

            else return null;
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
            for (int i = 0; i < 0x48; i++)
            {
                data[i] = ptr[i];
            }
            var str = string.Join(' ', data.Select(t => t.ToString("X")));
            Services.Log.Fatal(str);
        }

        public List<Gear.Materia> GetItemMateria(GameInventoryItem item)
        {
            var materiaIds = GetItemMateriaIds(item);
            var materiaList = new List<Gear.Materia>();
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

        public List<GearpiecePrerequesite> BuildGearpiecePrerequesites(uint gearpieceId)
        {
            // returns a list of item ids that are required to obtain the item with the provided id
            var prerequisites = new List<GearpiecePrerequesite>();

            // check for coffer
            if (ItemsCoffers.TryGetValue(gearpieceId, out var cofferId))
            {
                prerequisites.Add(new GearpiecePrerequesite(cofferId, this));
            }

            // no prerequesites in the table
            if (!ItemPrerequesites.TryGetValue(gearpieceId, out var directGearpiecePrereqs))
            {
                return prerequisites;
            }

            foreach (var prereqId in directGearpiecePrereqs)
            {
                var prereq = new GearpiecePrerequesite(prereqId, this);
                prerequisites.Add(prereq);
            }

            return prerequisites;
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
            Lumina.Excel.Sheets.Materia? materiaRow = null;
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

        public Gear.Materia BuildMateria(uint itemId)
        {

            var materiaName = GetItemNameById(itemId);

            var (statName, statLevel, statQuantity) = getMateriaInfo(materiaName);

            return new Gear.Materia(
                itemId,
                materiaName,
                statLevel,
                statName,
                statQuantity,
                false
                );
        }
    }
}
