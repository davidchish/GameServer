using System.Text.Json;
using GameShared;
using GameServer.WebSocketing;
using GameServer.Domain;

namespace GameServer.Messaging.Handlers;

public sealed class LoginHandler : IMessageHandler
{
    private readonly PlayerManager _players;
    public string Type => MessageTypes.Login;
    public LoginHandler(PlayerManager players) => _players = players;

    public async Task HandleAsync(Session session, string payloadJson, CancellationToken ct)
    {
        LoginRequest? req;
        try { req = JsonSerializer.Deserialize<LoginRequest>(payloadJson); }
        catch (Exception ex)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("BadRequest", ex.Message)), ct);
            return;
        }
        if (req is null || string.IsNullOrWhiteSpace(req.DeviceId))
        {
            await WebSocketServer.SendAsync(session, new Envelope("LoginResponse", new LoginResponse(Guid.Empty, "BadRequest: DeviceId missing")), ct);
            return;
        }

        var (ok, reason, state) = await _players.LoginAsync(session.SessionId, req.DeviceId, ct);
        if (!ok)
        {
            await WebSocketServer.SendAsync(session, new Envelope("LoginResponse", new LoginResponse(Guid.Empty, reason ?? "Failed")), ct);
            return;
        }
        session.PlayerId = state!.PlayerId;
        await WebSocketServer.SendAsync(session, new Envelope("LoginResponse", new LoginResponse(state.PlayerId, "OK")), ct);
    }
}
