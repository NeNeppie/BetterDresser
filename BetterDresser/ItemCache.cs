using System;
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
    public static ImmutableDictionary<uint, Item> CabinetItems = CabinetSheet
        .Where(item => item.RowId > 0)
        .ToImmutableDictionary(item => item.RowId, item => item.Item.Value);

    private static AgentMiragePrismPrismBox* AgentPrismBox => AgentMiragePrismPrismBox.Instance();
    private static FFXIVClientStructs.FFXIV.Client.Game.UI.Cabinet Cabinet => UIState.Instance()->Cabinet;

    private bool _scanArmoire = false;
    private DateTime _nextArmoireUpdate;

    public ItemCache()
    {
        // TODO: load from configuration
        //       Look into refreshing when adding/removing items from dresser?

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
        foreach (var item in CabinetItems)
        {
            if (!Cabinet.IsItemInCabinet(item.Key))
                continue;
            
            ArmoireItems.Add(new MirageArmoireItem(item.Key, item.Value));
        }

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
}
