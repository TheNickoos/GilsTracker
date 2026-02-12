using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace GilsTracker.Windows;

public class ConfigWindow : Window
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Gils Tracker - Configuration")
    {
        this.plugin = plugin;
        configuration = plugin.Configuration;

        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var showDtr = configuration.ShowDTR;
        if (ImGui.Checkbox("Show plugin on Server Info Bar", ref showDtr))
        {
            configuration.ShowDTR = showDtr;
            configuration.Save();

            plugin.ApplyDtrVisibility();
        }
    }
}
