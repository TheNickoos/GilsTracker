using Dalamud.Configuration;
using System;

namespace GilsTracker;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ShowDTR { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);

    }
}
