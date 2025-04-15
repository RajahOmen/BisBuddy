
namespace BisBuddy.EventListeners.AddonEventListeners.ShopExchange
{
    public class ShopExchangeCurrencyEventListener(Plugin plugin) : ShopExchangeEventListener(plugin)
    {
        // the type of shop this is (what do items in this shop cost?)
        public override string AddonName => "ShopExchangeCurrency";
        protected override float CustomNodeMaxY => 384;

        // ADDON ATKVALUE INDEXES
        protected override int AtkValueItemCountIndex => 4;
        protected override int AtkValueItemIdListStartingIndex => 1063;
        protected override int AtkValueFilteredItemsListStartingIndex => 1551;
        protected override uint AtkValueFilteredItemsListVisibleMaxValue => 1;

        // ADDON NODE IDS
        protected override uint AddonShopItemListNodeId => 19;
        protected override uint AddonShopHoverNodeId => 10;
        protected override uint AddonShopShieldHoverNodeId => 13;
        protected override uint AddonCustomHighlightNodeId => 420;
        protected override uint AddonScrollbarNodeId => 6;
        protected override uint AddonScrollbarButtonNodeId => 2;
        protected override uint AddonShieldInfoResNodeId => 3;
        protected override uint NoItemsTextNodeId => 20;
    }
}
