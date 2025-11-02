using System.Collections.Concurrent;
using GameShared;
using Serilog;

namespace GameServer.Domain;

public sealed class PlayerManager
{
    private readonly ConcurrentDictionary<string, PlayerState> _byDevice = new();
    private readonly ConcurrentDictionary<Guid, string> _deviceByPlayer = new();
    private readonly ConcurrentDictionary<Guid, Guid> _online = new(); // playerId -> sessionId
    private readonly ConcurrentDictionary<Guid, object> _locks = new();

    private object GetLock(Guid playerId) => _locks.GetOrAdd(playerId, _ => new object());

    public Task<(bool Ok, string? Reason, PlayerState? State)> LoginAsync(Guid sessionId, string deviceId, CancellationToken ct)
    {
        var state = _byDevice.GetOrAdd(deviceId, did => {
            var p = new PlayerState { PlayerId = Guid.NewGuid(), DeviceId = did };
            _deviceByPlayer[p.PlayerId] = did;
            return p;
        });

        if (_online.ContainsKey(state.PlayerId))
            return Task.FromResult<(bool, string?, PlayerState?)>((false, "Player already connected", null));

        _online[state.PlayerId] = sessionId;
        Log.Information("Player {PlayerId} logged in (device {DeviceId})", state.PlayerId, deviceId);
        return Task.FromResult<(bool, string?, PlayerState?)>((true, null, state));
    }

    public Task LogoutAsync(Guid sessionId, Guid? playerId)
    {
        if (playerId.HasValue)
            _online.TryRemove(playerId.Value, out _);
        return Task.CompletedTask;
    }

    public Task<PlayerState?> GetPlayerAsync(Guid playerId, CancellationToken ct)
    {
        if (_deviceByPlayer.TryGetValue(playerId, out var did) && _byDevice.TryGetValue(did, out var s))
            return Task.FromResult<PlayerState?>(s);
        return Task.FromResult<PlayerState?>(null);
    }

    public Task<bool> IsOnlineAsync(Guid playerId) => Task.FromResult(_online.ContainsKey(playerId));

    public Task<(bool Ok, int NewBalance)> UpdateResourceAsync(Guid playerId, ResourceType resourceType, int delta, CancellationToken ct)
    {
        if (!_deviceByPlayer.TryGetValue(playerId, out var did)) return Task.FromResult((false, 0));
        if (!_byDevice.TryGetValue(did, out var st)) return Task.FromResult((false, 0));

        lock (GetLock(playerId))
        {
            switch (resourceType)
            {
                case ResourceType.coins:
                    if (st.Coins + delta < 0) return Task.FromResult((false, st.Coins));
                    st.Coins += delta;
                    return Task.FromResult((true, st.Coins));
                case GameShared.ResourceType.rolls:
                    if (st.Rolls + delta < 0) return Task.FromResult((false, st.Rolls));
                    st.Rolls += delta;
                    return Task.FromResult((true, st.Rolls));
                default:
                    return Task.FromResult((false, 0));
            }
        }
    }
}
