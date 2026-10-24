using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using BetterDresser.Utilities;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace BetterDresser.Windows;

public class MainWindow : Window
{
    private static Vector2 IconSize => new(Service.PluginConfig.IconSize * ImGuiHelpers.GlobalScale);
    private static float DyeCircleRadius => IconSize.X / 12f;
    private static float DyeCircleThickness => IconSize.X / 48f;

    private List<MirageItemBase> _filteredItems = [];
    private EquipSlot _equipSlotFilter = EquipSlot.All;
    private uint _maxLevelFilter = Configuration.MaxEquipLevel;
    private uint _minLevelFilter = Configuration.MinEquipLevel;
    private bool _drawWindow = false;

    public MainWindow() : base("Better Dresser##betterdresser-main") => ToggleOn();

    private void ToggleOn() => _drawWindow = true;
    private void ToggleOff() => _drawWindow = false;

    public override void Draw()
    {
        if (!_drawWindow)
            return;

#if DEBUG
        ImGui.Text($"[{(ItemCache.IsDresserReady() ? 'V' : 'X')}] Items in the glamour dresser: {Service.ItemCache.DresserItems.Count}");
        ImGui.Text($"[{(ItemCache.IsArmoireReady() ? 'V' : 'X')}] Items in the armoire: {Service.ItemCache.ArmoireItems.Count}");
#endif

        if (ImGui.CollapsingHeader("Show Settings"))
            DrawConfig();

        // TODO: Display warning when armoire is not loaded

        using var child = ImRaii.Child("##betterdresser-items", default, true);
        if (!child)
            return;

        if (Service.ItemCache.UpdateReady)
        {
            Service.ItemCache.UpdateReady = false;
            RefreshItemList();   
        }
        if (Service.ItemCache.OwnedItems.Count == 0)
        {
            ImGui.TextColoredWrapped(new Vector4(1f, 0.6f, 0.6f, 1f), "Could not retrieve available items.");
            return;
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        using (var combo = ImRaii.Combo("##betterdresser-filter", _equipSlotFilter.GetDescription(), ImGuiComboFlags.HeightLargest))
        {
            if (combo)
            {
                foreach (var value in Enum.GetValues<EquipSlot>())
                {
                    if (ImGui.Selectable(value.GetDescription(), _equipSlotFilter == value))
                    {
                        _equipSlotFilter = value;
                        RefreshItemList();
                    }
                }
            }
        }

        // TODO: Search Bar!

        // From Dalamud's own Icon Browser implementation
        var itemsPerLine = (int)MathF.Floor(ImGui.GetContentRegionMax().X / (IconSize.X + ImGui.GetStyle().ItemSpacing.X));
        var lineHeight = IconSize.Y + ImGui.GetStyle().ItemSpacing.Y;

        ImGuiClip.ClippedDraw(_filteredItems, DrawItem, itemsPerLine, lineHeight);
    }

    private void DrawConfig()
    {
        if (ImGui.SliderUInt("Icon Size", ref Service.PluginConfig.IconSize, Configuration.IconSizeMin, Configuration.IconSizeMax, default, ImGuiSliderFlags.None))
            Service.PluginConfig.Save();

        ImGuiHelpers.ScaledDummy(5f);

        // TODO: Checkbox: Auto switch tabs when selecting slot in native dresser
        if (ImGui.Checkbox("Automatically change tabs when clicking on a slot", ref Service.PluginConfig.AutoSwitchTabs))
            Service.PluginConfig.Save();
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ErrorBackground, "Not Implemented");

        // TODO: Checkboxes: Highlight (frame) items from the dresser / armoire

        ImGuiHelpers.ScaledDummy(5f);

        ImGui.Text("Order items by...");
        var currentValue = ((SortingMode)Service.PluginConfig.ItemSortingMode).GetDescription();
        using (var combo = ImRaii.Combo("##sortingmode-combo", currentValue))
        {
            if (combo.Success)
            {
                var choices = Enum.GetValues<SortingMode>();
                foreach (var choice in choices)
                {
                    var label = choice.GetDescription();

                    if (ImGui.Selectable(label, label == currentValue))
                    {
                        Service.PluginConfig.ItemSortingMode = (byte)choice;
                        Service.ItemCache.ReorderItemLists();
                        RefreshItemList();
                        Service.PluginConfig.Save();
                    }
                }
            }
        }

        if (ImGui.InputUInt("Min Level", ref _minLevelFilter))
        {
            _minLevelFilter = Math.Min(Math.Max(_minLevelFilter, 1), 100);
            RefreshItemList();
        }
        if (ImGui.InputUInt("Max Level", ref _maxLevelFilter))
        {
            _maxLevelFilter = Math.Min(Math.Max(_maxLevelFilter, 1), 100);
            RefreshItemList();
        }
    }
    
    private void DrawItem(MirageItemBase item)
    {
        var cursorPos = ImGui.GetCursorScreenPos();

        var texture = ITextureProvider.Get().GetFromGameIcon(new(item.Icon));
        if (texture.TryGetWrap(out var wrap, out _))
        {
            ImGui.Image(wrap.Handle, IconSize, Vector2.Zero, Vector2.One, Vector4.One);
            cursorPos += IconSize;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{item.Source}\n{item.SourceId}: [{item.ItemId}] {item.Name}");

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            if (item.EquipmentSlot == EquipSlot.Fingers)
            {
                ImGui.OpenPopup($"##betterdresser-popup-left-{item.ItemId}");
            }
            else
            {
                item.ApplyToMiragePlate();
            }
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup($"##betterdresser-popup-right-{item.ItemId}");
        }

        // TEMP: Refactor needed
        if (item.DyeSlots != 0)
        {
            var drawlist = ImGui.GetWindowDrawList();
            var circleCenter = cursorPos + new Vector2(-DyeCircleRadius, (IconSize.Y / 8f) - IconSize.Y);

            drawlist.AddCircle(circleCenter, DyeCircleRadius, 0xFF000000u, DyeCircleThickness); 
            if (item.StainId1 != 0)
            {
                if (IDataManager.Get().GetExcelSheet<Stain>()!.TryGetRow(item.StainId1, out var stain))
                {
                    ImGui.GetWindowDrawList().AddCircleFilled(circleCenter, DyeCircleRadius, ImGuiUtils.ArgbToAbgr(stain.Color)); 
                }
            }

            if (item.DyeSlots == 2)
            {    
                circleCenter.Y += DyeCircleRadius * 2.5f;

                drawlist.AddCircle(circleCenter, DyeCircleRadius, 0xFF000000u, DyeCircleThickness); 
                if (item.StainId2 != 0)
                {
                    if (IDataManager.Get().GetExcelSheet<Stain>()!.TryGetRow(item.StainId2, out var stain))
                    {
                        ImGui.GetWindowDrawList().AddCircleFilled(circleCenter, DyeCircleRadius, ImGuiUtils.ArgbToAbgr(stain.Color));
                    }
                }
            }
        }

        DrawLeftClickPopup(item);
        DrawRightClickPopup(item);
    }

    private static void DrawItemName(MirageItemBase item) => ImGui.Text($"{item.Source}\n{item.SourceId}: [{item.ItemId}] {item.Name}");

    private static void DrawLeftClickPopup(MirageItemBase item)
    {
        using var popup = ImRaii.Popup($"##betterdresser-popup-left-{item.ItemId}");
        if (!popup) return;

        DrawItemName(item);

        if (ImGui.Selectable("Apply to Right Ring"))
            item.ApplyToMiragePlate(leftRing: false);

        if (ImGui.Selectable("Apply to Left Ring"))
            item.ApplyToMiragePlate(leftRing: true);
    }

    private static void DrawRightClickPopup(MirageItemBase item)
    {
        using var popup = ImRaii.Popup($"##betterdresser-popup-right-{item.ItemId}");
        if (!popup) return;

        DrawItemName(item);

        if (ImGui.Selectable("Try On"))
            AgentTryon.TryOn(0, item.ItemId, item.StainId1, item.StainId2);

        var url = $"https://garlandtools.org/db/#item/{item.ItemId}";
        if (ImGui.Selectable("Open in Garland Tools"))
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGuiUtils.HyperlinkTooltip(url);
    }

    private void RefreshItemList()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        _filteredItems = Service.ItemCache.OwnedItems
            .Where(item => _equipSlotFilter == EquipSlot.All || item.EquipmentSlot == _equipSlotFilter)
            .Where(item => item.EquipLevel >= _minLevelFilter && item.EquipLevel <= _maxLevelFilter)
            .ToList();

        stopwatch.Stop();
        IPluginLog.Get().Debug($"RefreshItemList() Filtered & reordered items in {stopwatch.Elapsed.Microseconds}μs");
    }
}
