using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using AgentMiragePrismPrismBox = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMiragePrismPrismBox;
using LuminaCabinet = Lumina.Excel.Sheets.Cabinet;
// using static FFXIVClientStructs.FFXIV.Client.UI.Agent.MiragePrismPrismBoxData;

namespace BetterDresser;

internal unsafe class ItemCache : IDisposable
{
    public List<MirageDresserItem> DresserItems = [];
    public List<MirageArmoireItem> ArmoireItems = [];
    public List<MirageItemBase> OwnedItems = [];
    public bool UpdateReady = false;

    public static ExcelSheet<LuminaCabinet> CabinetSheet => IDataManager.Get().GetExcelSheet<LuminaCabinet>();
    public static ImmutableDictionary<uint, Item> CabinetToItem = CabinetSheet
        .Where(item => item.RowId > 0)
        .ToImmutableDictionary(item => item.RowId, item => item.Item.Value);

    public static ImmutableDictionary<uint, uint> ItemToCabinet = CabinetSheet
        .Where(item => item.RowId > 0 && item.Item.RowId != 0)
        .ToImmutableDictionary(item => item.Item.RowId, item => item.RowId);

    private static AgentMiragePrismPrismBox* AgentPrismBox => AgentMiragePrismPrismBox.Instance();
    private static FFXIVClientStructs.FFXIV.Client.Game.UI.Cabinet Cabinet => UIState.Instance()->Cabinet;

    private bool _scanArmoire = false;
    private DateTime _nextArmoireUpdate;

    public ItemCache()
    {
        LoadArmoireFromConfig();
        // TODO: Look into refreshing when adding/removing items from dresser?
        // TODO: Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(46); // AgentMiragePrism, find tab corresponding with dresser/armoire
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostRequestedUpdate, "MiragePrismPrismBox", OnDresserPostUpdate);
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostSetup, ["Cabinet", "CabinetWithdraw"], EnableArmoireScan);
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostHide, ["Cabinet", "CabinetWithdraw"], DisableArmoireScan);
        IFramework.Get().Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        IFramework.Get().Update -= OnFrameworkUpdate;
        IAddonLifecycle.Get().UnregisterListener(AddonEvent.PostHide, ["Cabinet", "CabinetWithdraw"], DisableArmoireScan);
        IAddonLifecycle.Get().UnregisterListener(AddonEvent.PostSetup, ["Cabinet", "CabinetWithdraw"], EnableArmoireScan);
        IAddonLifecycle.Get().UnregisterListener(AddonEvent.PostRequestedUpdate, "MiragePrismPrismBox", OnDresserPostUpdate);
    }

    private void EnableArmoireScan(AddonEvent type, AddonArgs args) => _scanArmoire = true;
    private void DisableArmoireScan(AddonEvent type, AddonArgs args) => _scanArmoire = false;

    private void OnFrameworkUpdate(IFramework framework)
    {
        var update = framework.LastUpdate;

        if (_nextArmoireUpdate < update && _scanArmoire)
        {
            var ready = IsArmoireReady();
            if (ready)
                LoadFromArmoire();

            IPluginLog.Get().Verbose($"Relevant Framework Update @ {update} | Cabinet data ready: {ready}, state: {Cabinet.State}");
            _nextArmoireUpdate = DateTime.Now.AddMilliseconds(1500);
        }
    }

    private void OnDresserPostUpdate(AddonEvent type, AddonArgs args)
    {
        var ready = IsDresserReady();
        IPluginLog.Get().Debug($"OnDresser{type} called, data ready: {ready}");
        if (ready)
            LoadFromDresser();
    }

    public static bool IsDresserReady()
    {
        if (AgentPrismBox == null || !AgentPrismBox->IsAddonReady() || AgentPrismBox->Data is null)
            return false;
        return true;

        // return MirageManager.Instance()->PrismBoxLoaded;
    }

    public static bool IsArmoireReady()
    {
        return Cabinet.IsCabinetLoaded();
    }

    public void ReorderItemLists()
    {
        OwnedItems = OwnedItems.OrderBy(item =>
        {
            return Service.PluginConfig.ItemSortingMode switch
            {
                (byte)SortingMode.ItemLevelDescending => -item.ItemLevel,
                (byte)SortingMode.ItemLevelAscending => item.ItemLevel,
                (byte)SortingMode.ItemIdDescending => -item.ItemId,
                (byte)SortingMode.ItemIdAscending => item.ItemId,
                _ => -item.ItemId
            };
        }).ToList();
    }

    private void LoadFromDresser()
    {
        DresserItems.Clear();

        foreach (var item in AgentPrismBox->Data->PrismBoxItems)
        {
            // NOTE: For when I eventually port the UI to native, remember that outfits are filtered out at the loading stage
            if (item.ItemId == 0 || item.Slot >= 800 || item.EquipSlotCategory == 11)
                continue;

            DresserItems.Add(new MirageDresserItem(item));
        }

        // // MirageManager->PrismBoxItemIds
        // foreach(var itemId in MirageManager.Instance()->PrismBoxItemIds)
        // {
        //     if (itemId == 0)
        //         continue;

        //     DresserItemIds.Add(ItemUtil.GetBaseId(itemId).ItemId);
        // }
        
        CombineItemLists();
    }

    private void LoadFromArmoire()
    {
        ArmoireItems.Clear();
        foreach (var row in CabinetToItem)
        {
            if (!Cabinet.IsItemInCabinet(row.Key))
                continue;
            
            ArmoireItems.Add(new MirageArmoireItem(row.Key, row.Value));
        }

        SaveArmoireToConfig(); // Maybe only do this whenever ArmoireItems actually changes?
        CombineItemLists();
    }

    private void CombineItemLists()
    {
        OwnedItems.Clear();
        OwnedItems.AddRange(DresserItems);
        OwnedItems.AddRange(ArmoireItems);
        ReorderItemLists();
        UpdateReady = true;
    }

    // Doesn't *actually* Config.Save() because I don't want to write every 1.5 seconds
    private void SaveArmoireToConfig()
    {
        var bits = new BitArray(CabinetToItem.Count);
        foreach (var item in ArmoireItems)
        {
            if (ItemToCabinet.TryGetValue(item.ItemId, out var cabinetId))
                bits[(int)cabinetId] = true;
        }

        var bytes = new byte[((bits.Length - 1) / 8) + 1];
        bits.CopyTo(bytes, 0);
        Service.PluginConfig.ArmoireUnlockBits = Convert.ToHexString(bytes);
    }

    private void LoadArmoireFromConfig()
    {
        var bytes = Convert.FromHexString(Service.PluginConfig.ArmoireUnlockBits);
        var bits = new BitArray(bytes);
        for (int i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                var cabinetId = (uint)i;
                if (CabinetToItem.TryGetValue(cabinetId, out var item))
                    ArmoireItems.Add(new MirageArmoireItem(cabinetId, item));
            }
        }

        CombineItemLists();
    }
}
