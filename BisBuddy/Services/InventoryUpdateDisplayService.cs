using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class InventoryUpdateDisplayService : IInventoryUpdateDisplayService
    {
        public bool UpdateIsQueued { get; set; }
        public int GearpieceUpdateCount { get; set; }
        public bool IsManualUpdate { get; set; }
    }

    public interface IInventoryUpdateDisplayService
    {
        public bool UpdateIsQueued { get; set; }
        public int GearpieceUpdateCount { get; set; }
        public bool IsManualUpdate { get; set; }
    }
}
