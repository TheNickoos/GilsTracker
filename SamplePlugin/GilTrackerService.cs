using System;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;

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
    private const uint GilBaseItemId = 1;

    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IGameInventory _inventory;
    private DateTime _lastPoll = DateTime.MinValue;

    // Session state
    public bool HasBaseline => _baselineGil.HasValue;
    public int? CurrentGil => _currentGil;
    public int? BaselineGil => _baselineGil;
    public int SessionDelta => _sessionDelta;
    public int Gained => _gained;
    public int Spent => _spent;

    private int? _baselineGil;
    private int? _currentGil;
    private int _sessionDelta;
    private int _gained;
    private int _spent;

    public event Action<long, long, long>? OnGilChanged;
    // args = net, gained, spent

    public GilTrackerService(IClientState clientState, IFramework framework, IGameInventory inventory)
    {
        _clientState = clientState;
        _framework = framework;
        _inventory = inventory;

        _framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }

    public void ResetBaseline()
    {
        _baselineGil = null;
        _currentGil = null;
        _sessionDelta = 0;
        _gained = 0;
        _spent = 0;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        // Keep it light: poll ~2x/sec.
        var now = DateTime.UtcNow;
        if ((now - _lastPoll).TotalMilliseconds < 500)
            return;
        _lastPoll = now;

        if (!_clientState.IsLoggedIn)
        {
            // If you want persistence across character swaps you can remove this,
            // but for a simple "session" definition, we reset on logout.
            ResetBaseline();
            return;
        }

        var gil = TryReadCurrentGil();
        if (!gil.HasValue)
            return;

        if (!_baselineGil.HasValue)
        {
            _baselineGil = gil.Value;
            _currentGil = gil.Value;
            return;
        }

        bool changed = false;

        if (_currentGil.HasValue)
        {
            var diff = gil.Value - _currentGil.Value;
            if (diff != 0)
            {
                changed = true;

                if (diff > 0)
                    _gained += diff;
                else
                    _spent += -diff;
            }
        }

        _currentGil = gil.Value;
        _sessionDelta = gil.Value - _baselineGil.Value;

        if (changed)
            OnGilChanged?.Invoke(_sessionDelta, _gained, _spent);

    }

    private int? TryReadCurrentGil()
    {
        try
        {
            var items = _inventory.GetInventoryItems(GameInventoryType.Currency);
            foreach (ref readonly var item in items)
            {
                if (item.IsEmpty)
                    continue;
                if (item.BaseItemId == GilBaseItemId)
                    return item.Quantity;
            }
        }
        catch
        {
            // If Dalamud throws during zoning/loading, just ignore this tick.
        }

        return null;
    }

}
