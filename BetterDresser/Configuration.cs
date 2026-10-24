using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace BetterDresser;

public class Configuration : IPluginConfiguration
{
    [NonSerialized]
    internal const uint IconSizeMax = 100u;
    [NonSerialized]
    internal const uint IconSizeMin = 30u;
    [NonSerialized]
    internal const uint MaxEquipLevel = 100u;
    [NonSerialized]
    internal const uint MinEquipLevel = 1;

    public int Version { get; set; } = 0;

    public List<uint> ArmoireIdCache = [];
    public uint IconSize = 50u;
    public byte ItemSortingMode = (byte)SortingMode.ItemLevelDescending;
    public bool AutoSwitchTabs = false;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
