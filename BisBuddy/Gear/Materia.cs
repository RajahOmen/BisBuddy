using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    [Serializable]
    public class Materia
    {
        public readonly uint ItemId;
        public readonly string ItemName;
        public readonly string StatFullName;
        public readonly string StatShortName;
        public readonly int MateriaLevel;
        public readonly int StatQuantity;
        public bool IsMelded;

        public static readonly Dictionary<string, string> StatFullToShortName = new()
        {
            // DoW/DoM
            { "Critical Hit", "CRT" },
            { "Direct Hit Rate", "DHT" },
            { "Determination", "DET" },
            { "Skill Speed", "SKS" },
            { "Spell Speed", "SPS" },
            { "Piety", "PIE" },
            { "Tenacity", "TNC" },
            // DoH (abbrevs from etro)
            { "Craftsmanship", "CRFT" },
            { "Control", "CNTL" },
            { "CP", "CP" },
            // DoL (abbrevs from etro)
            { "Gathering", "GATH" },
            { "Perception", "PERC" },
            { "GP", "GP" },
        };

        public Materia(uint itemId, string itemName, int materiaLevel, string statFullName, int statQuantity, bool isMelded)
        {
            var statFullNameTrunc = statFullName.Length > 0
                ? statFullName[..Math.Min(3, statFullName.Length)]
                : "???";
            var statShortName = StatFullToShortName
                .GetValueOrDefault(
                    statFullName,
                    statFullNameTrunc
                );

            ItemId = itemId;
            IsMelded = isMelded;
            ItemName = itemName;
            StatFullName = statFullName;
            StatShortName = statShortName;
            MateriaLevel = materiaLevel;
            StatQuantity = statQuantity;
        }

        public static List<Materia> GetMatchingMateria(List<Materia> gearpieceMateria, List<uint> materiaIdList)
        {
            var newMateriaIdList = new List<uint>(materiaIdList);
            var matchingMateria = new List<Materia>();

            foreach (var materia in gearpieceMateria)
            {
                for (var i = 0; i < newMateriaIdList.Count; i++)
                {
                    if (newMateriaIdList[i] == materia.ItemId)
                    {
                        newMateriaIdList.RemoveAt(i);
                        matchingMateria.Add(materia);
                        break;
                    }
                }
            }

            return matchingMateria;
        }

        public static bool MateriaListCanSatisfy(List<Materia> availableList, List<Materia> requiredList)
        {
            // none required, any list satisfies
            if (requiredList.Count == 0)
                return true;

            // no restrictions on source, thus list satisfies
            if (availableList.Count == 0)
                return true;

            // returns true if the availableList has all the materia in the requiredList, false otherwise
            var remainingList = requiredList.Select(m => m.ItemId).ToList();

            for (var i = 0; i < availableList.Count; i++)
                remainingList.Remove(availableList[i].ItemId);

            return remainingList.Count == 0;
        }
    }
}
