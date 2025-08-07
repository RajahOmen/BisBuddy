using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Gear
{
    /// <summary>
    /// Represents something that can be collected by the user, such as
    /// a gearpiece, materia, or a prerequisite item
    /// </summary>
    public interface ICollectableItem
    {
        public CollectionStatusType CollectionStatus { get; }
    }
}
