
namespace BisBuddy.EventListeners.AddonEventListeners.ShopExchange
{
    internal class ShopExchangeItemEventListener(Plugin plugin) : ShopExchangeEventListenerBase(plugin)
    {
        public override string AddonName => "ShopExchangeItem";

        // ADDON ATKVALUE INDEXES
        // index of the number of items in shop
        protected override int AtkValueItemCountIndex => 3;
        // index of the first element in the item name list
        protected override int AtkValueItemNameListStartingIndex => 87;
        // index of the first element in the item id list
        protected override int AtkValueItemIdListStartingIndex => 1063;

        // ADDON NODE IDS
        // list of items in the shop
        protected override uint AddonShopItemListNodeId => 19;
        // amount of child nodes for a valid displayed item in a shop
        protected override int AddonShopItemChildNodeCount => 18;
        // node containing the name of the item in the shop
        protected override uint AddonShopItemTextNameNodeId => 7;
        // node for the hover highlight on the shop item
        protected override uint AddonShopHoverNode => 17;
        // node for the custom highlight on the shop item
        protected override uint AddonCustomHighlightNodeId => 420;
        // node for the scrollbar on the shop item list
        protected override uint AddonScrollbarNodeId => 6;
        // node for the button on the scrollbar
        protected override uint AddonScrollbarButtonNodeId => 2;
    }
}
