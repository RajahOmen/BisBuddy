using System;

namespace BisBuddy.Gear
{
    [Flags]
    [Serializable]
    public enum GearpieceType
    {
        None = 0,
        Weapon = 1 << 0,
        OffHand = 1 << 1,
        Head = 1 << 2,
        Body = 1 << 3,
        Hands = 1 << 4,
        Legs = 1 << 5,
        Feet = 1 << 6,
        Ears = 1 << 7,
        Neck = 1 << 8,
        Wrists = 1 << 9,
        Finger = 1 << 10,
        LeftSide = Head | Body | Hands | Legs | Feet,
        RightSide = Ears | Neck | Wrists | Finger,
    }
}
