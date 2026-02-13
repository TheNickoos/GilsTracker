using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GilsTracker.Windows;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;

namespace GilsTracker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInventory GameInventory { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;


    private const string CommandName = "/gilstracker";

    public Configuration Configuration { get; init; }

    internal GilTrackerService GilTracker { get; }

    public readonly WindowSystem WindowSystem = new("GilsTracker");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly IDtrBarEntry? dtrEntry;

    private SeString? lastDtrText;
    private string? lastDtrTooltip;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Polls Currency inventory on Framework.Update (throttled internally).
        GilTracker = new GilTrackerService(ClientState, PlayerState, Framework, GameInventory);

        GilTracker.OnGilChanged += UpdateDtrText;

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the GilsTracker window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        dtrEntry = DtrBar.Get("GilsTracker");

        dtrEntry.MinimumWidth = (ushort)170f;

        dtrEntry.Text = "Gil: …";
        dtrEntry.Tooltip = "GilsTracker\nClic: reset session";
        dtrEntry.Shown = Configuration.ShowDTR;

        dtrEntry.OnClick = _ =>
        {
            sessionStartUtc = DateTime.UtcNow;
            GilTracker.ResetBaseline();
        };



        Log.Information($"=== Loaded {PluginInterface.Manifest.Name} ===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        GilTracker.OnGilChanged -= UpdateDtrText;
        dtrEntry?.Remove();


        WindowSystem.RemoveAllWindows();

        GilTracker.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private DateTime sessionStartUtc = DateTime.UtcNow;

    private void UpdateDtrText(long net, long gained, long spent)
    {
        if (dtrEntry is null || !dtrEntry.Shown)
            return;

        var elapsedHours = (DateTime.UtcNow - sessionStartUtc).TotalHours;
        var perHour = elapsedHours > 0 ? (long)(net / elapsedHours) : 0;

        var color = (ushort)(net > 0 ? 45 : net < 0 ? 17 : 0);

        var text = new SeStringBuilder()
            .AddText("Gil ")
            .AddUiForeground($"{(net >= 0 ? "+" : "")}{FormatGil(net)}", color)
            .AddText($" | {FormatGil(perHour)}/h")
            .Build();

        var tooltip =
            $"GilsTracker\n" +
            $"Net: {(net >= 0 ? "+" : "")}{net:n0}\n" +
            $"Gained: +{gained:n0}\n" +
            $"Spent:  -{spent:n0}\n" +
            $"Rate: {perHour:n0} / hour\n" +
            $"Click: reset session";


        if (lastDtrText == null || !lastDtrText.Equals(text))
        {
            dtrEntry.Text = text;
            lastDtrText = text;
        }

        if (lastDtrTooltip != tooltip)
        {
            dtrEntry.Tooltip = tooltip;
            lastDtrTooltip = tooltip;
        }
    }


    private static string FormatGil(long v)
    {
        var a = Math.Abs(v);
        if (a >= 1_000_000) return $"{v / 1_000_000d:0.#}M";
        if (a >= 1_000) return $"{v / 1_000d:0.#}k";
        return v.ToString("0");
    }


    private void OnCommand(string command, string args) => MainWindow.Toggle();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    internal void ApplyDtrVisibility()
    {
        if (dtrEntry == null) return;

        dtrEntry.Shown = Configuration.ShowDTR;

        if (dtrEntry.Shown && GilTracker.HasBaseline)
            UpdateDtrText(GilTracker.SessionDelta, GilTracker.Gained, GilTracker.Spent);
    }


}
