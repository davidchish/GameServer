using System.Text.Json;
using GameShared;
using GameServer.WebSocketing;
using GameServer.Domain;

namespace GameServer.Messaging.Handlers;

public sealed class SendGiftHandler : IMessageHandler
{
    private readonly PlayerManager _players;
    public string Type => MessageTypes.SendGift;
    public SendGiftHandler(PlayerManager players) => _players = players;

    public async Task HandleAsync(Session session, string payloadJson, CancellationToken ct)
    {
        if (session.PlayerId is null)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("Unauthorized")), ct);
            return;
        }

        SendGiftRequest? req;
        try { req = JsonSerializer.Deserialize<SendGiftRequest>(payloadJson); }
        catch (Exception ex)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("BadRequest", ex.Message)), ct);
            return;
        }
        if (req is null || req.ResourceValue <= 0)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("BadRequest", "ResourceValue must be > 0")), ct);
            return;
        }

        var (senderOk, _) = await _players.UpdateResourceAsync(session.PlayerId.Value, req.ResourceType, -req.ResourceValue, ct);
        if (!senderOk)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("InvalidResourceOrBalance")), ct);
            return;
        }

        var friendState = await _players.GetPlayerAsync(req.FriendPlayerId, ct);
        if (friendState is null)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("FriendNotFound")), ct);
            return;
        }
        var (_, friendNew) = await _players.UpdateResourceAsync(req.FriendPlayerId, req.ResourceType, req.ResourceValue, ct);

        // In a real server, we would look up the friend's Session and send the event.
        // For this portfolio version, we just ACK to the sender.
        await WebSocketServer.SendAsync(session, new Envelope("SendGiftResponse", new { Status = "OK", FriendNewBalance = friendNew }), ct);
    }
}
