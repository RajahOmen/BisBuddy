namespace BisBuddy.Mediators
{
    public class InventoryUpdateDisplayMediator : IInventoryUpdateDisplayService
    {
        public bool UpdateIsQueued { get; set; } = false;
        public int GearpieceUpdateCount { get; set; } = -1;
        public bool IsManualUpdate { get; set; } = false;
    }

    public interface IInventoryUpdateDisplayService
    {
        public bool UpdateIsQueued { get; set; }
        public int GearpieceUpdateCount { get; set; }
        public bool IsManualUpdate { get; set; }
    }
}
