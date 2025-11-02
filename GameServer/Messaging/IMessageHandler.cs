using GameServer.WebSocketing;

namespace GameServer.Messaging;

public interface IMessageHandler
{
    string Type { get; }
    Task HandleAsync(Session session, string payloadJson, CancellationToken ct);
}
