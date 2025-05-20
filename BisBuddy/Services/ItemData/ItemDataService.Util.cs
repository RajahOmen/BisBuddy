using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using Dalamud.Game.Inventory;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GearMateria = BisBuddy.Gear.Materia;
using SheetMateria = Lumina.Excel.Sheets.Materia;

namespace BisBuddy.Items
{
    public partial class ItemDataService
    {
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

        public static string SeStringToString(ReadOnlySeString input)
        {
            return input.ExtractText().Replace("\u00AD", string.Empty);
        }

        public string GetItemNameById(uint id)
        {
            // check if item is HQ, change Id to NQ if it is
            var modifiedId = id;
            var itemIsHq = id >= ItemIdHqOffset;
            if (itemIsHq) modifiedId -= ItemIdHqOffset;

            // returns the name of the item with the provided id
            var itemName = SeStringToString(ItemSheet.GetRow(modifiedId).Name);

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
            var id = ItemSheet.FirstOrDefault(item => SeStringToString(item.Name) == name).RowId;

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
                    pluginLog.Error(e, $"Failed to get materia item id for item {GetItemNameById(item.ItemId)} (materia id {materiaId}, materia grade {materiaGrade})");
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
            pluginLog.Fatal(str);
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

        /// <summary>
        /// Returns if an item can have materia attached to it
        /// </summary>
        /// <param name="itemId">The item id to check</param>
        /// <returns>If it can be melded. If invalid item id, returns false.</returns>
        public bool ItemIsMeldable(uint itemId)
        {
            if (!tryGetItemRowById(itemId, out var itemRow))
                return false;

            return itemRow.MateriaSlotCount > 0;
        }

        /// <summary>   
        /// Extends the given PrerequisiteNode with new leaves at the node's direct child level according to current
        /// data. Used on config loading to automatically add new prerequisite information that wasn't available when the gearset
        /// was being added. Will never remove prerequisites, only add. It will also retain state for old nodes that already
        /// exist in the tree.
        /// </summary>
        /// <param name="itemId">The id of the item to extend prerequisites for</param>
        /// <param name="oldPrerequisiteNode">The old tree of prerequisites for this item</param>
        /// <param name="isCollected">If the config has this node set as collected</param>
        /// <param name="isManuallyCollected">If the config has this node set as manually collected</param>
        /// <returns>The most up-to-date PrerequisiteNode. Could be the unmodified original if no changes were made</returns>
        public IPrerequisiteNode? ExtendItemPrerequisites(uint itemId, IPrerequisiteNode? oldPrerequisiteNode, bool isCollected, bool isManuallyCollected)
        {

            // retrieve what current data has for this item
            var newPrerequisiteNode = BuildGearpiecePrerequisiteTree(itemId, isCollected, isManuallyCollected);

            // didn't have anything previously, return whatever we have now
            if (oldPrerequisiteNode == null)
                return newPrerequisiteNode;

            // somehow new data has no prerequisites, return what we had before
            if (newPrerequisiteNode == null)
                return oldPrerequisiteNode;

            // only want to add possibly-new alternatives, can only be when new is an OR node
            if (newPrerequisiteNode is not PrerequisiteOrNode)
                return oldPrerequisiteNode;

            // a new OR layer should be added
            if (oldPrerequisiteNode is not PrerequisiteOrNode)
            {
                // find node matching the old node in the new or node
                var oldNodeIndex = newPrerequisiteNode
                    .PrerequisiteTree
                    .FindIndex(node =>
                        node.ItemId == oldPrerequisiteNode.ItemId
                        && node.GetType() == oldPrerequisiteNode.GetType()
                    );

                pluginLog.Verbose($"New alternative found for \"{itemId}\", added as new {nameof(PrerequisiteOrNode)} layer");

                // add to new node. If no index found, insert at start
                if (oldNodeIndex >= 0)
                    newPrerequisiteNode.PrerequisiteTree[oldNodeIndex] = oldPrerequisiteNode;
                else
                    newPrerequisiteNode.PrerequisiteTree.Insert(0, oldPrerequisiteNode);

                return newPrerequisiteNode;
            }
            else // compare OR contents, add any new contents to old OR node
            {
                foreach (var newChildNode in newPrerequisiteNode.PrerequisiteTree)
                {
                    // add nodes that exist in new tree to old tree
                    var existsInOldNode = oldPrerequisiteNode
                        .PrerequisiteTree
                        .Any(node =>
                            node.ItemId == newChildNode.ItemId
                            && node.GetType() == newChildNode.GetType()
                        );

                    if (!existsInOldNode)
                    {
                        pluginLog.Verbose($"New alternative found for \"{itemId}\", added to existing {nameof(PrerequisiteOrNode)} layer");
                        oldPrerequisiteNode.PrerequisiteTree.Add(newChildNode);
                    }

                }
                return oldPrerequisiteNode;
            }
        }

        public IPrerequisiteNode? BuildGearpiecePrerequisiteTree(uint itemId, bool isCollected = false, bool isManuallyCollected = false)
        {
            var unitPrerequisiteGroup = buildPrerequisites(itemId, isCollected: isCollected, isManuallyCollected: isManuallyCollected);

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

        private IPrerequisiteNode buildPrerequisites(uint itemId, bool isCollected, bool isManuallyCollected, int depth = 8)
        {
            var itemName = GetItemNameById(itemId);
            var group = new PrerequisiteAtomNode(itemId, itemName, [], PrerequisiteNodeSourceType.Item, isCollected, isManuallyCollected);

            if (depth <= 0)
                return group;

            // build list of prerequisites from supplementals data
            IPrerequisiteNode supplementalTree = new PrerequisiteOrNode(itemId, itemName, [], PrerequisiteNodeSourceType.Loot);
            var supplementalPrereqs = ItemsCoffers[itemId];
            var hasSupplementalPrereqs = supplementalPrereqs.Any();

            if (supplementalPrereqs.Count() == 1)
            {
                supplementalTree = buildPrerequisites(supplementalPrereqs.First(), isCollected, isManuallyCollected, depth - 1);
                supplementalTree.SourceType = PrerequisiteNodeSourceType.Loot;
            }
            else if (supplementalPrereqs.Count() > 1)
            {
                supplementalTree.PrerequisiteTree = ItemsCoffers[itemId]
                    .Select(id => buildPrerequisites(id, isCollected, isManuallyCollected, depth - 1))
                    .ToList();
            }

            // build list of prerequisites from shops/exchanges data
            IPrerequisiteNode exchangesTree = new PrerequisiteOrNode(itemId, itemName, [], PrerequisiteNodeSourceType.Shop);
            var exchangesPrereqs = ItemsPrerequisites[itemId];
            var hasExchangesPrereqs = exchangesPrereqs.Any();

            // only exchangable at one shop listing
            if (exchangesPrereqs.Count() == 1)
            {
                var shopCosts = exchangesPrereqs.First();

                // shop only is requesting one item to exchange
                if (shopCosts.Count == 1)
                {
                    exchangesTree = buildPrerequisites(shopCosts.First(), isCollected, isManuallyCollected, depth - 1);
                    exchangesTree.SourceType = PrerequisiteNodeSourceType.Shop;
                }
                else // shop is requesting more than one item to exchange
                {
                    var prereqTree = shopCosts
                        .Select(id => buildPrerequisites(id, isCollected, isManuallyCollected, depth - 1))
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
                            var prereq = buildPrerequisites(shopCostIds.First(), isCollected, isManuallyCollected, depth - 1);
                            prereq.SourceType = PrerequisiteNodeSourceType.Shop;
                            return prereq;
                        }

                        // Shop costs multiple items
                        return new PrerequisiteAndNode(
                            itemId,
                            itemName,
                            shopCostIds.Select(id => buildPrerequisites(id, isCollected, isManuallyCollected, depth - 1)).ToList(),
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

        private (uint statId, string statName, int statLevel, int statQuantity) getMateriaInfo(string materiaName)
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
                    if (SeStringToString(col.Value.Name) == materiaName)
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

            var statName = SeStringToString(materiaRow.Value.BaseParam.Value.Name);
            var statQuantity = materiaRow.Value.Value[materiaCol];

            return (materiaRow.Value.RowId, statName, materiaCol, statQuantity);
        }

        public Gearpiece BuildGearpiece(
            uint itemId,
            IPrerequisiteNode? prerequisiteTree,
            List<GearMateria> itemMateria,
            bool isCollected = false,
            bool isManuallyCollected = false
            )
        {
            return new Gearpiece(
                itemId,
                GetItemNameById(itemId),
                GetItemGearpieceType(itemId),
                prerequisiteTree,
                itemMateria,
                isCollected,
                isManuallyCollected
                );
        }

        public GearMateria BuildMateria(uint itemId, bool isMelded = false)
        {
            var materiaName = GetItemNameById(itemId);
            var (statId, statName, statLevel, statQuantity) = getMateriaInfo(materiaName);

            return new GearMateria(
                itemId,
                materiaName,
                statLevel,
                statId,
                statName,
                statQuantity,
                isMelded
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
