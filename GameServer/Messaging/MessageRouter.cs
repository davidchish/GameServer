using Serilog;
using GameServer.WebSocketing;

namespace GameServer.Messaging;

public sealed class MessageRouter
{
    private readonly IReadOnlyDictionary<string, IMessageHandler> _handlers;

    public MessageRouter(IEnumerable<IMessageHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.OrdinalIgnoreCase);
    }

    public async Task DispatchAsync(Session session, string type, string payloadJson, CancellationToken ct)
    {
        if (_handlers.TryGetValue(type, out var h))
        {
            await h.HandleAsync(session, payloadJson, ct);
            return;
        }
        Log.Warning("Unknown message type: {Type}", type);
        await WebSocketServer.SendAsync(session, new GameShared.Envelope("Error", new GameShared.ErrorResponse("UnknownType", type)), ct);
    }
}
