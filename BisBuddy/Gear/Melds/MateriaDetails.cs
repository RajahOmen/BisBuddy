using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Gear.Melds
{
    public struct MateriaDetails
    {
        /// <summary>
        /// The RowId of the materia in the "Item" table.
        /// </summary>
        public uint ItemId;

        /// <summary>
        /// The RowId of the materia type in the "Materia" table
        /// </summary>
        public uint MateriaId;

        /// <summary>
        /// The name of the materia, in the current game language
        /// e.g. "Quicktongue Materia I"
        /// </summary>
        public string ItemName;

        /// <summary>
        /// The name of the stat the materia provides, in the current game language
        /// e.g. "Spell Speed"
        /// </summary>
        public string StatName;

        /// <summary>
        /// The type of the materia, which determines the stat it provides.
        /// e.g. "MateriaType.SpellSpeed"
        /// </summary>
        public MateriaStatType StatType;

        /// <summary>
        /// The level of the materia, displayed in game as the roman
        /// numeral equivalent of this value. The level determines the
        /// strength of the stat.
        /// e.g. "10", "11", "12"
        /// </summary>
        public int Level;

        /// <summary>
        /// The amount of the stat provided by this materia.
        /// e.g. "36", "18", "56"
        /// </summary>
        public int Strength;
    }
}
