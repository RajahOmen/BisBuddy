using System;
using System.Collections.Generic;

namespace BisBuddy.Gear
{
    internal static class GearpieceTypeMapper
    {
        private static readonly Dictionary<string, GearpieceType> Mapping = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Weapon", GearpieceType.Weapon },
            { "MainHand", GearpieceType.Weapon },
            { "weapons", GearpieceType.Weapon },        // savage upgrade material
            { "weaponry", GearpieceType.Weapon },       // criterion savage upgrade material

            { "OffHand", GearpieceType.OffHand },

            { "Head", GearpieceType.Head },
            { "Kabuto", GearpieceType.Head },           // Deltascape
            { "headgear", GearpieceType.Head },         // normal raid gear tokens

            { "Body", GearpieceType.Body },
            { "Chest", GearpieceType.Body },
            { "Armor", GearpieceType.Body },            // Deltascape
            { "body gear", GearpieceType.Body },        // normal raid gear tokens

            { "Hands", GearpieceType.Hands },
            { "Hand", GearpieceType.Hands },
            { "Gloves", GearpieceType.Hands },
            { "Kote", GearpieceType.Hands },            // Deltascape
            { "arm gear", GearpieceType.Hands },        // normal raid gear tokens

            { "Legs", GearpieceType.Legs },
            { "Leg", GearpieceType.Legs },
            { "Tsutsuhakama", GearpieceType.Legs },     // Deltascape
            { "leg gear", GearpieceType.Legs },         // normal raid gear tokens

            { "Feet", GearpieceType.Feet },
            { "Suneate", GearpieceType.Feet },          // Deltascape
            { "Foot", GearpieceType.Feet },
            { "foot gear", GearpieceType.Feet },        // normal raid gear tokens

            { "Ears", GearpieceType.Ears },
            { "Earring", GearpieceType.Ears },

            { "Neck", GearpieceType.Neck },
            { "Necklace", GearpieceType.Neck },

            { "Wrists", GearpieceType.Wrists },
            { "Wrist", GearpieceType.Wrists },
            { "Bracelet", GearpieceType.Wrists },

            { "Finger", GearpieceType.Finger },
            { "Ring", GearpieceType.Finger },
            { "Ring1", GearpieceType.Finger },
            { "Ring2", GearpieceType.Finger },
            { "RingLeft", GearpieceType.Finger },
            { "RingRight", GearpieceType.Finger },
            { "FingerL", GearpieceType.Finger },        // etro
            { "FingerR", GearpieceType.Finger },        // etro

            { "LeftSide", GearpieceType.LeftSide },
            { "vestments", GearpieceType.LeftSide },    // savage upgrade material

            { "RightSide", GearpieceType.RightSide },
            { "accessories", GearpieceType.RightSide }, // savage upgrade material
            { "accessory", GearpieceType.RightSide },   // normal raid gear tokens
        };

        public static GearpieceType Parse(string input)
        {
            if (Mapping.TryGetValue(input, out var gearpieceType)) return gearpieceType;

            throw new ArgumentException($"Invalid gear piece type: {input}");
        }

        public static bool TryParse(string input, out GearpieceType gearpieceType)
        {
            return Mapping.TryGetValue(input, out gearpieceType);
        }
    }
}
