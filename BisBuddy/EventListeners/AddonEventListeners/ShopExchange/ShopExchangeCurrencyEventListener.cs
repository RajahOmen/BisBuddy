
namespace BisBuddy.EventListeners.AddonEventListeners.ShopExchange
{
    public class ShopExchangeCurrencyEventListener(Plugin plugin) : ShopExchangeEventListener(plugin)
    {
        // the type of shop this is (what do items in this shop cost?)
        public override string AddonName => "ShopExchangeCurrency";

        // ADDON ATKVALUE INDEXES
        protected override int AtkValueItemCountIndex => 4;
        protected override int AtkValueItemNameListStartingIndex => 87;
        protected override int AtkValueItemIdListStartingIndex => 1063;

        // ADDON NODE IDS
        protected override uint AddonShopItemListNodeId => 19;
        protected override uint AddonShopHoverNodeId => 10;
        protected override uint AddonCustomHighlightNodeId => 420;
        protected override uint AddonScrollbarNodeId => 6;
        protected override uint AddonScrollbarButtonNodeId => 2;
    }
}
