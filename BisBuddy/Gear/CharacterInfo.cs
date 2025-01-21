using System;
using System.Collections.Generic;

namespace BisBuddy.Gear
{
    [Serializable]
    public struct CharacterInfo(
        ulong characterId,
        List<Gearset> gearsets
        )
    {
        // LocalContentId of the character
        public ulong CharacterId = characterId;
        public List<Gearset> Gearsets = gearsets;
    }
}
