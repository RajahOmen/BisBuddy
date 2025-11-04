using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Resources;
using BisBuddy.Util;
using Dalamud.Game;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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

            return id + Constants.ItemIdHqOffset;
        }

        public string SeStringToString(ReadOnlySeString input)
        {
            return input.ExtractText().Replace("\u00AD", string.Empty);
        }

        public string GetItemNameById(uint id)
        {
            // check if item is HQ, change Id to NQ if it is
            var modifiedId = id;
            var itemIsHq = id >= Constants.ItemIdHqOffset;
            if (itemIsHq) modifiedId -= Constants.ItemIdHqOffset;

            // returns the name of the item with the provided id
            var itemName = SeStringToString(ItemSheet.GetRow(modifiedId).Name);

            // add Hq icon to the item name if it is hq
            if (itemIsHq) itemName = $"{itemName} {Constants.HqIcon}";

            NameToId[itemName] = id;
            return itemName;
        }

        private bool tryGetItemRowById(uint itemId, out Item item)
        {
            if (itemId > Constants.ItemIdHqOffset)
                itemId -= Constants.ItemIdHqOffset;

            return ItemSheet.TryGetRow(itemId, out item);
        }

        public uint GetItemIdByName(string name)
        {
            // return cached value
            if (NameToId.TryGetValue(name, out var value)) return value;

            var itemIsHq = name.Contains(Constants.HqIcon);
            if (itemIsHq) name = name[..^2]; // remove hq icon (remove hq icon and space)

            // get from item sheet if not cached
            var id = ItemSheet.FirstOrDefault(item => SeStringToString(item.Name) == name).RowId;

            // convert to HQ id if item is HQ
            if (itemIsHq) id += Constants.ItemIdHqOffset;

            NameToId[name] = id;
            return id;
        }

        public string GetShopNameById(uint shopId)
        {
            if (!ShopSheet.TryGetRow(shopId, out var shop))
                return string.Empty;

            return SeStringToString(shop.Name);
        }

        public uint GetMateriaItemId(ushort materiaId, byte materiaGrade)
        {
            try
            {
                // can fail for weird gear, like Eternal Ring//
                if (!Materia.TryGetRow(materiaId, out var materiaRow))
                    return 0;

                // row is materiaId (the type: crt, det, etc), column is materia grade (I, II, III, etc)
                var materiaItem = materiaRow.Item[materiaGrade];

                return materiaItem.RowId;
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to get materia item id for materia id {materiaId}, materia grade {materiaGrade}");
                throw;
            }
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

        public ItemUICategory GetItemUICategory(uint itemId)
        {

            if (!tryGetItemRowById(itemId, out var itemRow))
                throw new ArgumentException($"Invalid itemId {itemId}");

            return itemRow.ItemUICategory.Value;
        }

        public ushort GetItemIconId(uint itemId)
        {
            if (!tryGetItemRowById(itemId, out var itemRow))
                throw new ArgumentException($"Invalid itemId {itemId}");

            return itemRow.Icon;
        }

        public int GetItemMateriaSlotCount(uint itemId)
        {
            if (!tryGetItemRowById(itemId, out var itemRow))
                throw new ArgumentException($"Invalid itemId {itemId}");
            return itemRow.MateriaSlotCount;
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
                    .Index()
                    .First(node =>
                        node.Item.GetType() == oldPrerequisiteNode.GetType()
                        && sameItemRequirements(node.Item, oldPrerequisiteNode)
                    ).Index;

                logger.Debug($"New alternative found for \"{itemId}\", added as new {nameof(PrerequisiteOrNode)} layer (idx: {oldNodeIndex})");

                // add to new node. If no index found, insert at start
                if (oldNodeIndex >= 0)
                    newPrerequisiteNode.ReplaceNode(oldNodeIndex, oldPrerequisiteNode);
                else
                    newPrerequisiteNode.InsertNode(0, oldPrerequisiteNode);

                return newPrerequisiteNode;
            }
            else // compare OR contents, add any new contents to old OR node
            {
                foreach (var newChildNode in newPrerequisiteNode.PrerequisiteTree)
                {
                    var newChildNodeItemIds = newChildNode
                        .GetItemRequirements(includeDisabledNodes: true)
                        .Select(req => req.ItemId);

                    // add nodes that exist in new tree to old tree
                    var existsInOldNode = ((PrerequisiteOrNode)oldPrerequisiteNode)
                        .CompletePrerequisiteTree
                        .Any(entry =>
                            entry.Node.GetItemRequirements(includeDisabledNodes: true).Select(req => req.ItemId).SequenceEqual(newChildNodeItemIds)
                            && entry.Node.GetType() == newChildNode.GetType()
                        );

                    if (!existsInOldNode)
                    {
                        logger.Debug($"New alternative found for \"{itemId}\", added to existing {nameof(PrerequisiteOrNode)} layer");
                        logger.Debug($"NEW: {string.Join(", ", newChildNodeItemIds)}");
                        foreach (var node in ((PrerequisiteOrNode)oldPrerequisiteNode).CompletePrerequisiteTree)
                        {
                            logger.Debug($"OLD: {string.Join(", ", node.Node.GetItemRequirements(includeDisabledNodes: true).Select(r => r.ItemId))}");
                        }
                        oldPrerequisiteNode.AddNode(newChildNode);
                    }

                }
                return oldPrerequisiteNode;
            }
        }

        private static bool sameItemRequirements(IPrerequisiteNode node1, IPrerequisiteNode node2)
        {
            var items1 = node1.GetItemRequirements(includeDisabledNodes: true);
            var items2 = node2.GetItemRequirements(includeDisabledNodes: true);

            return items1.Count() == items2.Count() && !items1.Except(items2).Any();
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
            IPrerequisiteNode supplementalTree;
            var supplementalPrereqs = ItemsCoffers[itemId];
            var hasSupplementalPrereqs = supplementalPrereqs.Any();

            if (supplementalPrereqs.Count() == 1)
            {
                supplementalTree = buildPrerequisites(supplementalPrereqs.First().ItemId, isCollected, isManuallyCollected, depth - 1);
                supplementalTree.SourceType = PrerequisiteNodeSourceType.Loot;
            }
            else if (supplementalPrereqs.Count() > 1)
            {
                var prereqs = ItemsCoffers[itemId]
                    .Select(entry => buildPrerequisites(entry.ItemId, isCollected, isManuallyCollected, depth - 1))
                    .ToList();

                supplementalTree = new PrerequisiteOrNode(
                    itemId,
                    itemName,
                    prereqs,
                    PrerequisiteNodeSourceType.Loot
                    );
            }
            else
            {
                supplementalTree = new PrerequisiteOrNode(itemId, itemName, [], PrerequisiteNodeSourceType.Loot);
            }

            // build list of prerequisites from shops/exchanges data
            IPrerequisiteNode exchangesTree;
            var exchangesPrereqs = ItemsPrerequisites[itemId];
            var hasExchangesPrereqs = exchangesPrereqs.Any();

            // only exchangable at one shop listing
            if (exchangesPrereqs.Count() == 1)
            {
                var shopCosts = exchangesPrereqs.First().ItemIds;

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
                var prereqs = exchangesPrereqs
                    .Select(entries =>
                    {
                        var shopCostIds = entries.ItemIds;
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

                exchangesTree = new PrerequisiteOrNode(
                    itemId,
                    itemName,
                    prereqs,
                    PrerequisiteNodeSourceType.Shop
                    );
            }
            else
            {
                exchangesTree = new PrerequisiteOrNode(itemId, itemName, [], PrerequisiteNodeSourceType.Shop);
            }

            // build resulting group
            if (
                hasSupplementalPrereqs
                && hasExchangesPrereqs
                )
            {

                // try to compact the tree, if possible
                var compositeTree = new List<IPrerequisiteNode>();
                if (supplementalTree is PrerequisiteOrNode)
                    foreach (var suppPrereq in supplementalTree.PrerequisiteTree)
                    {
                        suppPrereq.SourceType = supplementalTree.SourceType;
                        compositeTree.Add(suppPrereq);
                    }
                else
                    compositeTree.Add(supplementalTree);

                if (exchangesTree is PrerequisiteOrNode)
                    foreach (var excPrereq in exchangesTree.PrerequisiteTree)
                    {
                        excPrereq.SourceType = exchangesTree.SourceType;
                        compositeTree.Add(excPrereq);
                    }
                else
                    compositeTree.Add(exchangesTree);

                // exchangesTree of both shop/exchange sources and supplemental sources
                var compoundGroup = new PrerequisiteOrNode(
                    itemId,
                    itemName,
                    compositeTree,
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
                var exchangeNames = exchangesPrereqs.SelectMany(prereqs => prereqs.ItemIds).Select(GetItemNameById);
                var prereqNames = exchangesTree.PrerequisiteTree.SelectMany(prereq => prereq.PrerequisiteTree).Select(p => p.ItemName);
                // only from shops/exchanges
                group.PrerequisiteTree = [exchangesTree];
            }

            return group;
        }

        public MateriaDetails GetMateriaInfo(uint materiaItemId)
        {
            if (MateriaDetailsCache.TryGetValue(materiaItemId, out var value))
                return value;

            var materiaName = GetItemNameById(materiaItemId);

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

            if (materiaRow is not SheetMateria materiaRowValue || materiaCol < 0)
                throw new InvalidOperationException($"Materia {materiaName} not found in materia sheet");

            var statName = SeStringToString(materiaRowValue.BaseParam.Value.Name);
            var statType = (MateriaStatType)materiaRowValue.RowId;
            var statQuantity = materiaRowValue.Value[materiaCol];

            var materiaDetails = new MateriaDetails()
            {
                ItemId = materiaItemId,
                MateriaId = materiaRowValue.RowId,
                ItemName = materiaName,
                StatName = statName,
                StatType = statType,
                Level = materiaCol,
                Strength = statQuantity
            };
            MateriaDetailsCache.Add(materiaItemId, materiaDetails);
            return materiaDetails;
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
                        if (gearpieceTypeMapper.TryParse(property.Name, out var type))
                            return type;
                    }
                }
            }

            // none found
            return GearpieceType.None;
        }

        public ClassJobInfo GetClassJobInfoByEnAbbreviation(string abbrevation, ClientLanguage language = ClientLanguage.English)
        {
            var sheet = language == ClientLanguage.English
                ? ClassJobEn
                : dataManager.GetExcelSheet<ClassJob>(language);

            foreach (var row in sheet)
            {
                if (row.Abbreviation.ExtractText().Equals(abbrevation, StringComparison.InvariantCultureIgnoreCase))
                    return new(
                        classJobId: row.RowId,
                        name: row.Name.ExtractText(),
                        abbreviation: row.Abbreviation.ExtractText()
                        );
            }

            return nullJobInfo;
        }

        public ClassJobInfo GetClassJobInfoById(uint jobId)
        {
            if (jobId == 0 || !ClassJobEn.TryGetRow(jobId, out var row))
                return nullJobInfo;

            var titleCaseName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(row.Name.ExtractText().ToLower());

            return new(
                classJobId: row.RowId,
                name: titleCaseName,
                abbreviation: row.Abbreviation.ExtractText()
                );
        }

        private int classJobCount =>
            // +1 because "no job" is not included in the count
            ClassJobEn.Count(c => !c.Name.ExtractText().IsNullOrEmpty()) + 1;

        private ClassJobInfo nullJobInfo => new(
            classJobId: 0,
            name: Resource.UnknownClassJobName,
            abbreviation: Resource.UnknownClassJobAbbreviation,
            iconIdIndex: classJobCount + Constants.CompanionIconOffset
            );

        public IEnumerable<uint> FindClassJobIdUsers(IEnumerable<uint> itemIds)
        {
            var allClassJobIds = ClassJobEn
                .Where(job => !job.Name.IsEmpty)
                .Select(job => job.RowId);

            var validJobs = itemIds.Aggregate(allClassJobIds, (validJobIds, nextItemId) =>
            {
                if (!tryGetItemRowById(nextItemId, out var item))
                    return validJobIds;

                var categoryRow = dataManager
                    .GetExcelSheet<RawRow>(ClientLanguage.English, "ClassJobCategory")
                    .GetRow(item.ClassJobCategory.RowId);

                return validJobIds
                    .Where(id => categoryRow.ReadBoolColumn((int)id + 1));
            });

            return validJobs;
        }
    }
}
