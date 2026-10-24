using System;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace BetterDresser;

public abstract record MirageItemBase
{
    public string Name { get; init; } = "";
    public abstract AgentMiragePrismMiragePlateData.ItemSource Source { get; }
    public EquipSlot EquipmentSlot { get; init; }
    public uint SourceId { get; init; }
    public uint ItemId { get; init; }
    public uint Icon { get; init; }
    public uint EquipLevel { get; init; }
    public uint ItemLevel { get; init; }
    public uint DyeSlots = 0;
    public byte StainId1 { get; init; } = 0;
    public byte StainId2 { get; init; } = 0;

    // TODO: Fix items initially applying only after clicking twice
    public unsafe void ApplyToMiragePlate(bool leftRing = false)
    {
        var agent = AgentMiragePrismMiragePlate.Instance();
        if (agent is null)
            return;

        if (agent->Data is var data and not null)
        {
            var originalSlot = data->SelectedItemIndex;
            data->SelectedItemIndex = EquipmentSlot == EquipSlot.Fingers && leftRing ? (uint)EquipSlot.Fingers + 1u : (uint)EquipmentSlot;
            agent->SetSelectedItemData(Source, SourceId, ItemId, StainId1, StainId2);

            data->SelectedItemIndex = originalSlot;
            data->HasChanges = true;
        }
    }
}

public record MirageDresserItem : MirageItemBase
{
    public override AgentMiragePrismMiragePlateData.ItemSource Source => AgentMiragePrismMiragePlateData.ItemSource.PrismBox;

    public MirageDresserItem(PrismBoxItem prismItem)
    {
        Name = prismItem.Name.ExtractText();
        EquipmentSlot = (EquipSlot)prismItem.EquipSlotCategory;
        SourceId = prismItem.Slot;
        ItemId = ItemUtil.GetBaseId(prismItem.ItemId).ItemId;
        Icon = ItemUtil.GetBaseId(prismItem.IconId).ItemId;
        EquipLevel = prismItem.LevelEquip;
        ItemLevel = prismItem.ItemLevel;
        StainId1 = prismItem.Stains[0];
        StainId2 = prismItem.Stains[1];
        
        var equipRestriction = (MirageEquipRestriction)prismItem.EquipRestriction;
        DyeSlots = equipRestriction.HasFlag(MirageEquipRestriction.DyeSlot2) ? 2u :
                   equipRestriction.HasFlag(MirageEquipRestriction.DyeSlot1) ? 1u : 0;
    }
}

public record MirageArmoireItem : MirageItemBase
{
    public override AgentMiragePrismMiragePlateData.ItemSource Source => AgentMiragePrismMiragePlateData.ItemSource.Cabinet;

    public MirageArmoireItem(uint rowId, Item item)
    {
        Name = item.Name.ExtractText();
        EquipmentSlot = MapToEnum(item.EquipSlotCategory.RowId);
        SourceId = rowId;
        ItemId = item.RowId;
        Icon = item.Icon;
        EquipLevel = item.LevelEquip;
        ItemLevel = item.LevelItem.RowId;
        DyeSlots = item.DyeCount;
    }

    private static EquipSlot MapToEnum(uint equipSlotCategory)
    {
        return equipSlotCategory switch
        {
            1 or 13 => EquipSlot.MainHand,
            2 => EquipSlot.OffHand,
            3 => EquipSlot.Head,
            4 or 15 or 16 or (>= 19 and <= 23) => EquipSlot.Body,
            5 => EquipSlot.Hands,
            7 or 18 => EquipSlot.Legs,
            8 => EquipSlot.Feet,
            9 => EquipSlot.Ears,
            10 => EquipSlot.Neck,
            11 => EquipSlot.Wrists,
            12 => EquipSlot.Fingers,
            _ => throw new ArgumentOutOfRangeException(nameof(equipSlotCategory), equipSlotCategory, "")
        };
    }
}
