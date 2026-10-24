using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace BetterDresser.Utilities;

public static class ImGuiUtils
{
    public static Vector4 GetDefaultColor(ImGuiCol col) => ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(col));

    public static Vector4 TextColor => GetDefaultColor(ImGuiCol.Text);
    public static Vector4 TextColor50 => TextColor with { W = 0.5f };

    // https://github.com/Haselnussbomber/HaselCommon/blob/main/HaselCommon/Gui/ImGuiUtils.cs#L36
    public static void HyperlinkTooltip(string url)
    {
        using var tooltip = ImRaii.Tooltip();

        ImGui.GetWindowDrawList().AddText(
            UiBuilder.IconFont, 12 * ImGuiHelpers.GlobalScale,
            ImGui.GetCursorScreenPos() + new Vector2(2 * ImGuiHelpers.GlobalScale),
            ImGui.ColorConvertFloat4ToU32(TextColor50),
            FontAwesomeIcon.ExternalLinkAlt.ToIconString()
        );

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (20 * ImGuiHelpers.GlobalScale));

        ImGui.TextColored(TextColor50, url);
    }

    public static uint ArgbToAbgr(uint value)
    {
        value <<= 8;

        // Endian flip
        return (BitOperations.RotateRight(value & 0x00FF00FFu, 8)
            + BitOperations.RotateLeft(value & 0xFF00FF00u, 8))
            | 0xFF000000u;
    }
}
