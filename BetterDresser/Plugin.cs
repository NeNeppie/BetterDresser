using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

using BetterDresser.Windows;
#if DEBUG
using BetterDresser.Debug;
#endif

namespace BetterDresser;

/*
NOTE: Information:

Internal Name: Better Dresser

- Plugins that keep track of items stored in the dresser and armoire do it via the Config
- For fetching out of the dresser:
  - AgentMiragePrismPrismBox.Instance()->Data->PrismBoxItems (Includes outfits, custom struct)
  - MirageManager.Instance()->PrismBoxItemIds (Condenses outfits into an single item, bleh)
- For fetching out of the armoire:
  - UIState "Cabinet" and method IsItemInCabinet()

- Data availability:
  - Dresser: AgentMiragePrismPrismBox remembers data only when in dresser menues
  -          MirageManager ?
  - Armoire: UIState's Cabinet makes server request to load data when accessing the armoire in any way, but not when accessing just the dresser

- Armoire Icon: ui/uId/img06/ItemDetailPutIn_hr1.tex[5] / [2]

- ItemFinderModule as a cache is unreliable for this plugin's purposes
*/

public sealed class Plugin : IDalamudPlugin
{
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    private const string CommandName = "/glamore";
    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private MainWindow MainWindow { get; init; }
#if DEBUG
    private static Inspector Inspector { get; set; } = null!;
#endif

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
        PluginInterface.Create<Service>();
        MainWindow = new(); // Service needs to be created before MainWindow

#if DEBUG
        // Inspector = new();
#endif
        WindowSystem.AddWindow(MainWindow);

        ICommandManager.Get().AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

        WindowSystem.RemoveAllWindows();
#if DEBUG
        // Inspector.Dispose();
#endif
        ICommandManager.Get().RemoveHandler(CommandName);
    }

    public void ToggleMainUi() => MainWindow.Toggle();
    private void OnCommand(string command, string args) => ToggleMainUi();
}
