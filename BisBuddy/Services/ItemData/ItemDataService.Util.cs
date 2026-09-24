using BisBuddy.Extensions;
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
using System.Collections;
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

        public bool ItemIdIsHq(uint itemId) =>
            itemId >= Constants.ItemIdHqOffset;

        public int GetPercentChanceToAttach(uint materiaGrade, int overmeldSlotIdx, bool attachToHq)
        {
            if (!dataManager.GetExcelSheet<MateriaGrade>().TryGetRow(materiaGrade, out var materiaInfo))
                return 0;

            if (attachToHq)
                return materiaInfo.OvermeldHQPercent[overmeldSlotIdx];
            else
                return materiaInfo.OvermeldNQPercent[overmeldSlotIdx];
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

        public (int Normal, int Advanced) GetItemMateriaSlotCount(uint itemId)
        {
            if (!tryGetItemRowById(itemId, out var itemRow))
                throw new ArgumentException($"Invalid itemId {itemId}");
            var advancedCount = itemRow.IsAdvancedMeldingPermitted
                ? 5 - itemRow.MateriaSlotCount
                : 0;
            return (itemRow.MateriaSlotCount, advancedCount);
        }

        public void ExtendItemPrerequisites(
            PrerequisiteNode? oldPrerequisiteNode,
            int maxDepth = 8
            )
        {
            if (oldPrerequisiteNode is not null)
                logger.Verbose($"Explicitly called extend for node: {oldPrerequisiteNode.ItemName} ({oldPrerequisiteNode.NodeId})...");
            extendPrerequisiteNode(oldPrerequisiteNode, depth: maxDepth);
            return;
        }

        public PrerequisiteNode BuildGearpiecePrerequisiteTree(
            uint itemId,
            bool isCollected = false,
            bool isManuallyCollected = false,
            int maxDepth = 8
            )
        {
            var itemNode = buildPrerequisites(
                itemId,
                isCollected: isCollected,
                isManuallyCollected: isManuallyCollected,
                parentItemIds: new HashSet<uint>(),
                depth: maxDepth
            );
            return itemNode;
        }

        private PrerequisiteNode buildPrerequisites(uint itemId, bool isCollected, bool isManuallyCollected, IReadOnlySet<uint> parentItemIds, int depth = 8)
        {
            var itemNode = new PrerequisiteNode(
                itemId: itemId,
                itemName: GetItemNameById(itemId),
                completePrerequisiteTree: null,
                isCollected: isCollected,
                collectLock: isManuallyCollected,
                isMeldable: ItemIsMeldable(itemId)
            );
            extendPrerequisiteNode(
                prerequisiteNode: itemNode,
                depth: depth
            );
            return itemNode;
        }

        private void extendPrerequisiteNode(
            PrerequisiteNode? prerequisiteNode,
            int depth = 8
        )
        {
            if (depth <= 0 || prerequisiteNode is null)
                return;

            logger.Verbose($"Extending prerequisite node {prerequisiteNode.ItemName} ({prerequisiteNode.NodeId}) with max depth {depth}...\n{prerequisiteNode}");

            var nodesToPopulate = new List<(PrerequisiteNode node, HashSet<uint> parentItemIds, int depth)>() { (prerequisiteNode, [], 1) };
            var maxIterations = 1000;
            var i = 0;
            while (nodesToPopulate.Count > 0 && i++ < maxIterations)
            {
                var (node, nodeParentIds, nodeDepth) = nodesToPopulate.First();
                nodesToPopulate.RemoveAt(0);
                var logPrefix = new string(' ', nodeDepth * 4);
                var nodeParentIdsStr = string.Join(", ", nodeParentIds);

                logger.Verbose($"{logPrefix[..^2]}============= depth = {nodeDepth}");
                logger.Verbose($"{logPrefix}Finding children for node ({node.NodeId}):\n{node}");

                if (nodeDepth >= depth)
                {
                    logger.Verbose($"{logPrefix}Max depth reached: {nodeDepth} >= {depth}");
                    continue;
                }

                // find prerequisite sources from gear coffers
                var supplementalSources = ItemsCoffers[node.ItemId];
                var supplementalNodes = new List<PrerequisiteNode>();
                logger.Verbose($"{logPrefix}Searching through {supplementalSources.Count()} ({node.ItemId}) supplemental sources");
                foreach (var (sourceItemId, sourceType) in supplementalSources)
                {
                    if (nodeParentIds.Contains(sourceItemId))
                    {
                        logger.Verbose($"{logPrefix}skipping {sourceItemId}, in [{nodeParentIdsStr}]");
                        continue;
                    }

                    var supNode = new PrerequisiteNode(
                        itemId: sourceItemId,
                        itemName: GetItemNameById(sourceItemId),
                        completePrerequisiteTree: null,
                        isCollected: prerequisiteNode.IsCollected,
                        collectLock: prerequisiteNode.CollectLock,
                        isMeldable: ItemIsMeldable(sourceItemId)
                    );
                    logger.Verbose($"{logPrefix}Supplemental source {supNode.ItemName} ({supNode.ItemId})");

                    supplementalNodes.Add(supNode);
                }

                // find prerequisite sources from NPC shops
                var exchangeSources = ItemsPrerequisites[node.ItemId];
                var exchangeCostLists = new List<List<PrerequisiteNode>>();
                logger.Verbose($"{logPrefix}Searching through {exchangeSources.Count()} ({node.ItemId}) exchange sources");
                foreach (var (shopCosts, sourceShopId) in exchangeSources)
                {
                    var shopCostsStr = string.Join(", ", shopCosts);
                    if (nodeParentIds.IsSupersetOf(shopCosts) || shopCosts.Count == 0)
                    {
                        logger.Verbose($"{logPrefix}skipping [{shopCostsStr}], all in [{nodeParentIdsStr}]");
                        continue;
                    }

                    var costNodes = new List<PrerequisiteNode>();
                    logger.Verbose($"{logPrefix}Shop with costs ({shopCosts.Count}) [{shopCostsStr}]");
                    foreach (var costItemId in shopCosts)
                    {
                        var costNode = new PrerequisiteNode(
                            itemId: costItemId,
                            itemName: GetItemNameById(costItemId),
                            completePrerequisiteTree: null,
                            isCollected: prerequisiteNode.IsCollected,
                            collectLock: prerequisiteNode.CollectLock,
                            isMeldable: ItemIsMeldable(costItemId)
                        );
                        logger.Verbose($"{logPrefix}Supplemental source {costNode.ItemName} ({costNode.ItemId})");
                        costNodes.Add(costNode);
                    }
                    exchangeCostLists.Add(costNodes);
                }

                List<PrerequisiteAndGroup> newPrereqGroups = [];

                // build node to match prerequisites
                logger.Verbose($"{logPrefix}{supplementalNodes.Count} supplemental nodes, {exchangeCostLists.Count} exchange cost lists");
                foreach (var supNode in supplementalNodes)
                {
                    newPrereqGroups.Add(new PrerequisiteAndGroup(
                        prerequisites: [supNode],
                        sourceType: PrerequisiteNodeSourceType.Loot
                    ));
                }

                foreach (var exchangeCosts in exchangeCostLists)
                {
                    newPrereqGroups.Add(new PrerequisiteAndGroup(
                        prerequisites: exchangeCosts,
                        sourceType: PrerequisiteNodeSourceType.Shop
                    ));
                }

                if (newPrereqGroups.Count == 0)
                {
                    if (node.CompletePrerequisiteTree.Count > 0)
                        logger.Verbose($"{logPrefix}Somehow, no new prerequisites but has {node.CompletePrerequisiteTree.Count} old, trying to extend old");
                    else
                        logger.Verbose($"{logPrefix}No prerequisites, not extending at all");
                }
                else
                {
                    if (node.CompletePrerequisiteTree.Count > 0)
                    {
                        logger.Verbose($"{logPrefix}Previous nodes in tree, have to extend logic");
                        var prevTreeStr = string.Join(", ", node.CompletePrerequisiteTree.Select(g => $"{Enum.GetName(g.SourceType)}{g.Prerequisites.Count}"));
                        var newTreeStr = string.Join(", ", newPrereqGroups.Select(g => $"{Enum.GetName(g.SourceType)}{g.Prerequisites.Count}"));
                        logger.Verbose($"{logPrefix}Previous tree: [{prevTreeStr}] New Tree: [{newTreeStr}]");

                        var oldGroups = node.CompletePrerequisiteTree;
                        List<PrerequisiteAndGroup> oldNewGroups = [.. newPrereqGroups];

                        foreach (var newGroup in oldNewGroups)
                        {
                            logger.Verbose($"{logPrefix}Checking old groups for match to new group:\n{newGroup}");
                            if (oldGroups.Any(g => g.Equals(newGroup)))
                            {
                                logger.Verbose($"{logPrefix}Group found in old group, ignoring");
                                newPrereqGroups.Remove(newGroup);
                            }
                            else
                            {
                                logger.Verbose($"{logPrefix}New group found, extending as new OR");
                            }
                        }
                    }

                    logger.Verbose($"{logPrefix}adding {newPrereqGroups.Count} groups to prerequisite tree");
                    foreach (var newGroup in newPrereqGroups)
                    {
                        logger.Verbose($"{logPrefix}Adding group to tree:\n{newGroup}");

                        node.AddGroup(newGroup);
                    }
                }

                logger.Verbose($"{logPrefix}adding {node.CompletePrerequisiteNodes.Count()} prereq nodes to populate list");
                foreach (var prereq in node.CompletePrerequisiteNodes)
                {
                    nodesToPopulate.Add((prereq, [.. nodeParentIds, node.ItemId], nodeDepth + 1));
                }
            }


            if (i >= maxIterations)
                logger.Warning($"Warning: extend prerequisite for item {prerequisiteNode.ItemName} hit max iterations, this shouldn't happen");
            else
                logger.Verbose($"Extend complete after {i} loops");

            logger.Debug($"Extended node result:\n{prerequisiteNode}");
            return;
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

        public ClassJobInfo GetClassJobInfoByEnAbbreviation(string abbrevation, ClientLanguage? language = null)
        {
            foreach (var row in ClassJobSheetEn)
            {
                var rowAbbrev = row.Abbreviation.ExtractText();
                if (rowAbbrev.Equals(abbrevation, StringComparison.InvariantCultureIgnoreCase))
                {
                    var displayRow = row;

                    // try to use localized version for name/abbreviation
                    if (
                        (language ?? dataManager.Language) != ClientLanguage.English
                        && ClassJobSheet.TryGetRow(row.RowId, out var localLangRow)
                    )
                        displayRow = localLangRow;

                    logger.Verbose($"name: {displayRow.Name.ExtractText()}, abbrev: {displayRow.Abbreviation.ExtractText()}");

                    return new(
                        classJobId: row.RowId,
                        name: getTitleCaseClassJobName(row),
                        abbreviation: displayRow.Abbreviation.ExtractText()
                    );
                }
            }

            return nullJobInfo;
        }

        public ClassJobInfo GetClassJobInfoById(uint jobId)
        {
            if (jobId == 0 || !ClassJobSheet.TryGetRow(jobId, out var row))
                return nullJobInfo;

            return new(
                classJobId: row.RowId,
                name: getTitleCaseClassJobName(row),
                abbreviation: row.Abbreviation.ExtractText()
                );
        }

        private string getTitleCaseClassJobName(ClassJob classJobRow) =>
            CultureInfo
                .CurrentCulture
                .TextInfo
                .ToTitleCase(
                    classJobRow.Name.ExtractText().ToLower(CultureInfo.CurrentCulture)
                );

        private int classJobCount =>
            // +1 because "no job" is not included in the count
            ClassJobSheetEn.Count(c => !c.Name.ExtractText().IsNullOrEmpty()) + 1;

        private ClassJobInfo nullJobInfo => new(
            classJobId: 0,
            name: Resource.UnknownClassJobName,
            abbreviation: Resource.UnknownClassJobAbbreviation,
            iconIdIndex: classJobCount + Constants.CompanionIconOffset
            );

        public IEnumerable<uint> FindClassJobIdUsers(IEnumerable<uint> itemIds)
        {
            var allClassJobIds = ClassJobSheetEn
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
