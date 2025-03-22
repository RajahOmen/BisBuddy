using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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

        public static readonly Dictionary<uint, string> StatFullToShortName = new()
        {
            // DoW/DoM
            { 14, "DHT" },
            { 15, "CRT" },
            { 16, "DET" },
            {  7, "PIE" },
            { 17, "TNC" },
            { 24, "SKS" },
            { 25, "SPS" },
            // DoL (abbrevs from etro)
            { 18, "GATH" },
            { 19, "PERC" },
            { 20, "GP" },
            // DoH (abbrevs from etro)
            { 21, "CRFT" },
            { 22, "CP" },
            { 23, "CNTL" },
        };

        public Materia(
            uint itemId,
            string itemName,
            int materiaLevel,
            uint statId,
            string statFullName,
            int statQuantity,
            bool isMelded
            )
        {
            var statFullNameTrunc = statFullName.Length > 0
                ? statFullName[..Math.Min(3, statFullName.Length)]
                : "???";
            var statShortName = StatFullToShortName
                .GetValueOrDefault(
                    statId,
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

        [JsonConstructor]
        public Materia(
            uint itemId,
            string itemName,
            int materiaLevel,
            string statShortName,
            string statFullName,
            int statQuantity,
            bool isMelded
            )
        {
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
