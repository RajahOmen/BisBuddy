

namespace BisBuddy.Gear.Prerequesites
{
    public enum PrerequesiteNodeSourceType
    {
        Item,     // this is the 'base' item, cannot be obtained from supported sources
        Compound, // more than one source type
        Shop,     // from a NPC shop/exchange
        Loot      // from a loot coffer, or other items that players can use to turn into other items
    }
}
