using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Gear
{
    /// <summary>
    /// Represents the different states of collection a collectable item can be in
    /// </summary>
    public enum CollectionStatusType
    {
        /// <summary>
        /// An item is marked collected and all sub-items (like materia) are marked collected.
        /// </summary>
        ObtainedComplete,

        /// <summary>
        /// An item is marked collected, but some sub-items (like materia) are not marked collected.
        /// </summary>
        ObtainedPartial,

        /// <summary>
        /// An item is not marked collected, but it can be collected (via trade-in or prerequisites are collected)
        /// </summary>
        Obtainable,

        /// <summary>
        /// An item is not marked collected and cannot be determined to be collectable
        /// </summary>
        NotObtainable,
    }
}
