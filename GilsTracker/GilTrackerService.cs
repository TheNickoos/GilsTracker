using Dalamud.Game.Inventory;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;

namespace GilsTracker;

/// <summary>
/// Tracks gil changes during the current game session.
/// 
/// Implementation note:
/// We read the Currency inventory (GameInventoryType.Currency) and look for the entry
/// whose BaseItemId == 1 (Gil). The Quantity of that entry equals the player's current gil.
/// </summary>
internal sealed class GilTrackerService : IDisposable
{
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

    private const uint GilBaseItemId = 1;

    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameInventory inventory;
    private DateTime lastPoll = DateTime.MinValue;

    // Session state
    public bool HasBaseline => baselineGil.HasValue;
    public int? CurrentGil => currentGil;
    public int? BaselineGil => baselineGil;
    public int SessionDelta => sessionDelta;
    public int Gained => gained;
    public int Spent => spent;

    private int? baselineGil;
    private int? currentGil;
    private int sessionDelta;
    private int gained;
    private int spent;

    public event Action<long, long, long>? OnGilChanged;
    // args = net, gained, spent

    private readonly IPlayerState playerState;

    public GilTrackerService(IClientState _clientState, IPlayerState _playerState, IFramework _framework, IGameInventory _inventory)
    {
        clientState = _clientState;
        playerState = _playerState;
        framework = _framework;
        inventory = _inventory;

        framework.Update += OnFrameworkUpdate;
    }


    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
    }

    public void ResetBaseline()
    {
        baselineGil = null;
        currentGil = null;
        sessionDelta = 0;
        gained = 0;
        spent = 0;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        // Keep it light: poll ~2x/sec.
        var now = DateTime.UtcNow;
        if ((now - lastPoll).TotalMilliseconds < 500)
            return;
        lastPoll = now;

        if (!clientState.IsLoggedIn)
        {
            ResetBaseline();
            return;
        }

        var gil = TryReadCurrentGil();
        if (!gil.HasValue)
            return;

        if (!baselineGil.HasValue)
        {
            baselineGil = gil.Value;
            currentGil = gil.Value;
            return;
        }

        var changed = false;

        if (currentGil.HasValue)
        {
            var diff = gil.Value - currentGil.Value;
            if (diff != 0)
            {
                changed = true;

                if (diff > 0)
                    gained += diff;
                else
                    spent += -diff;
            }
        }

        currentGil = gil.Value;
        sessionDelta = gil.Value - baselineGil.Value;

        if (changed)
            OnGilChanged?.Invoke(sessionDelta, gained, spent);

    }

private int? TryReadCurrentGil()
{
    if (!clientState.IsLoggedIn)
        return null;

    if (ObjectTable.LocalPlayer == null)
         return null;

    var items = inventory.GetInventoryItems(GameInventoryType.Currency);
    foreach (ref readonly var item in items)
    {
        if (item.IsEmpty) continue;
        if (item.BaseItemId == GilBaseItemId)
            return item.Quantity;
    }

    return null;
}


}
