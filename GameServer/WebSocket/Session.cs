using System.Net.WebSockets;

namespace GameServer.WebSocketing;

public sealed class Session
{
    public Guid SessionId { get; } = Guid.NewGuid();
    public Guid? PlayerId { get; set; }
    public WebSocket Socket { get; }
    public Session(WebSocket socket) => Socket = socket;
}
