using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;
using System.Reflection;

namespace GilsTracker.Windows;

public class MainWindow : Window
{
    private readonly Plugin plugin;
    private readonly ISharedImmediateTexture? logo;

    public MainWindow(Plugin plugin)
        : base("Gils Tracker##GilsTracker", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = "GilsTracker.GilsTracker.png";

        logo = Plugin.TextureProvider.GetFromManifestResource(asm, resourceName);

        this.plugin = plugin;
    }

    public override void Draw()
    {
        // Top bar
        if (ImGui.Button("Config"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        if (ImGui.Button("Reset session"))
            plugin.GilTracker.ResetBaseline();

        ImGui.Spacing();

        using (var child = ImRaii.Child("Header", new Vector2(0, 95 * ImGuiHelpers.GlobalScale), true))
        {
            if (child.Success)
            {
                if (logo != null)
                {
                    ImGui.Image(logo.GetWrapOrEmpty().Handle, new Vector2(64, 64));
                    ImGui.SameLine();
                }

                ImGui.BeginGroup();
                ImGui.TextUnformatted("Gils Tracker");
                ImGui.TextDisabled("Tracks gil gained/spent since login.");
                ImGui.EndGroup();
            }
        }

        ImGuiHelpers.ScaledDummy(6);

        // Main stats
        if (!Plugin.ClientState.IsLoggedIn)
        {
            ImGui.TextDisabled("Not logged in.");
            return;
        }

        var t = plugin.GilTracker;
        if (!t.HasBaseline || t.CurrentGil is null)
        {
            ImGui.TextDisabled("Initializing... (waiting for first gil read)");
            return;
        }

        var startGil = t.BaselineGil ?? 0;
        var currentGil = t.CurrentGil ?? 0;

        ImGui.Text($"Start:   {startGil:N0} gil");
        ImGui.Text($"Current: {currentGil:N0} gil");

        ImGui.Separator();

        ImGui.Text($"Net:     {t.SessionDelta:N0} gil");
        ImGui.Text($"Gained:  {t.Gained:N0} gil");
        ImGui.Text($"Spent:   {t.Spent:N0} gil");
    }
}
