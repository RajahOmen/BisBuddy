using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    [Serializable]
    public class GearpiecePrerequesite
    {
        private static readonly int MaxPrerequesiteDepth = 5;
        private bool isManuallyCollected = false;
        private bool isCollected = false;
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public List<GearpiecePrerequesite> Prerequesites { get; set; }
        public int PrerequesiteCount => Prerequesites.Sum(p => 1 + p.PrerequesiteCount);
        public bool IsCollected
        {
            get => isCollected;
            private set
            {
                isCollected = value;
                // if this item is collected, then all its prerequesites are also collected
                if (value)
                {
                    foreach (var p in Prerequesites)
                    {
                        p.IsCollected = true;
                    }
                }
            }
        }
        public bool IsManuallyCollected
        {
            get => isManuallyCollected;
            private set => isManuallyCollected = value;
        }

        public GearpiecePrerequesite(
            uint itemId,
            ItemData itemData,
            bool isCollected = false
            ) : this(itemId, itemData, MaxPrerequesiteDepth, isCollected)
        { }

        [System.Text.Json.Serialization.JsonConstructor]
        [Newtonsoft.Json.JsonConstructor]
        public GearpiecePrerequesite(
            uint itemId,
            string itemName,
            List<GearpiecePrerequesite> prerequesites,
            bool isCollected,
            bool isManuallyCollected
            )
        {
            ItemId = itemId;
            ItemName = itemName;
            Prerequesites = prerequesites;
            IsCollected = isCollected;
            IsManuallyCollected = isManuallyCollected;
        }

        private GearpiecePrerequesite(uint itemId, ItemData itemData, int depth = 0, bool isCollected = false)
        {
            ItemId = itemId;
            ItemName = itemData.GetItemNameById(itemId);
            IsCollected = isCollected;
            Prerequesites = [];

            if (depth > 0 && itemData.ItemPrerequesites.TryGetValue(itemId, out var prereqs))
            {
                Prerequesites = prereqs
                    .Select(p => new GearpiecePrerequesite(p, itemData, depth - 1, isCollected))
                    .ToList();
            }
        }

        public void SetCollected(bool collected, bool manualToggle)
        {
            if (IsManuallyCollected && !collected && !manualToggle)
            {
                Services.Log.Error($"Cannot automatically uncollect manually collected item: {ItemName}");
                return;
            }

            IsCollected = collected;

            // if toggled by user, set manually collected flag
            if (manualToggle) IsManuallyCollected = collected;
        }
    }
}
