using System;

namespace BetterDresser;

public enum EquipSlot : uint
{
    All = 256,
    MainHand = 0,
    OffHand,
    Head,
    Body,
    Hands,
    Legs,
    Feet,
    Ears,
    Neck,
    Wrists,
    Fingers,
}

public enum SortingMode : byte
{
    ItemLevelDescending = 0,
    ItemLevelAscending,
    ItemIdDescending,
    ItemIdAscending,
}

/* 
PrismBoxItem.EquipRestriction flags, tested as female Au Ra:

    Male Same Race 2 Dyes    - 87  | 1 2 4 0 16 00 64 000
    Female Diff Race 2 Dyes  - 101 | 1 0 4 0 00 32 64 000
    Female Same Race 2 Dyes  - 100 | 0 0 4 0 00 32 64 000

    Male Diff Race 1 Dye     - 23  | 1 2 4 0 16 00 00 000
    Female Diff Race 1 Dye   - 37  | 1 0 4 0 00 32 00 000
    Female Same Race 1 Dye   - 36  | 0 0 4 0 00 32 00 000

    Male 1 Dye               - 23  | 1 2 4 0 16 00 00 000
    Female 1 Dye             - 36  | 0 0 4 0 00 32 00 000

    All No Dyes              - 48  | 0 0 0 0 16 32 00 000
    All 1 Dye                - 52  | 0 0 4 0 16 32 00 000 
    All 2 Dyes               - 116 | 0 0 4 0 16 32 64 000

    Male No Dyes             - 19  | 1 2 0 0 16 00 00 000
    Female No Dyes           - 32  | 0 0 0 0 00 32 00 000 ?
*/
[Flags]
public enum MirageEquipRestriction : byte
{
    None            = 0,
    Unk01           = 1, // CannotEquip?
    Unk02           = 2, // CannotEquip2?
    DyeSlot1        = 4,
    Unk08           = 8,
    MaleCanEquip    = 16,
    FemaleCanEquip  = 32,
    DyeSlot2        = 64,
}

internal static class EnumExtensions
{
    internal static string GetDescription(this EquipSlot value)
    {
        return value switch
        {
            EquipSlot.All => "Display All",
            EquipSlot.MainHand => "Main Hand",
            EquipSlot.OffHand => "Off Hand",
            EquipSlot.Head => "Head",
            EquipSlot.Body => "Body",
            EquipSlot.Hands => "Hands",
            EquipSlot.Legs => "Legs",
            EquipSlot.Feet => "Feet",
            EquipSlot.Ears => "Ears",
            EquipSlot.Neck => "Neck",
            EquipSlot.Wrists => "Wrists",
            EquipSlot.Fingers => "Fingers",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "")
        };
    }

    internal static string GetDescription(this SortingMode value)
    {
        return value switch
        {
            SortingMode.ItemIdDescending => "Internal Id (Descending)",
            SortingMode.ItemIdAscending => "Internal Id (Ascending)",
            SortingMode.ItemLevelDescending => "Item Level (Descending)",
            SortingMode.ItemLevelAscending => "Item Level (Ascending)",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "")
        };
    }
}
