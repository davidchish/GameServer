using System;
using System.Text.Json;
using GameShared;
using GameServer.WebSocketing;
using GameServer.Domain;

namespace GameServer.Messaging.Handlers;

public sealed class SendGiftHandler : IMessageHandler
{
    private readonly PlayerManager _players;
    public string Type => MessageTypes.SendGift;

    private readonly SessionManager _sessions;
    public SendGiftHandler(PlayerManager players, SessionManager sessions)
    {
        _players = players;
        _sessions = sessions;
    }

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


        var friendSessionId = await _players.GetOnlineSessionIdAsync(req.FriendPlayerId);
        if (friendSessionId is Guid fid && _sessions.TryGetSession(fid, out var friendSession) && friendSession is not null)
        {
            var giftEvent = new GiftEvent(session.PlayerId.Value, req.ResourceType, req.ResourceValue, DateTime.UtcNow);
            await WebSocketServer.SendAsync(friendSession, new Envelope("GiftEvent", giftEvent), ct);
        }

        await WebSocketServer.SendAsync(session, new Envelope("SendGiftResponse", new { Status = "OK", FriendNewBalance = friendNew }), ct);
    }
}
