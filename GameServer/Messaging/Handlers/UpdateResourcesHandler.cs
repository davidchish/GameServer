using System.Text.Json;
using GameShared;
using GameServer.WebSocketing;
using GameServer.Domain;

namespace GameServer.Messaging.Handlers;

public sealed class UpdateResourcesHandler : IMessageHandler
{
    private readonly PlayerManager _players;
    public string Type => MessageTypes.UpdateResources;
    public UpdateResourcesHandler(PlayerManager players) => _players = players;

    public async Task HandleAsync(Session session, string payloadJson, CancellationToken ct)
    {
        if (session.PlayerId is null)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("Unauthorized")), ct);
            return;
        }

        UpdateResourcesRequest? req;
        try { req = JsonSerializer.Deserialize<UpdateResourcesRequest>(payloadJson); }
        catch (Exception ex)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("BadRequest", ex.Message)), ct);
            return;
        }
        if (req is null)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("BadRequest")), ct);
            return;
        }

        var (ok, newBal) = await _players.UpdateResourceAsync(session.PlayerId.Value, req.ResourceType, req.ResourceValue, ct);
        if (!ok)
        {
            await WebSocketServer.SendAsync(session, new Envelope("Error", new ErrorResponse("InvalidResourceOrBalance")), ct);
            return;
        }
        await WebSocketServer.SendAsync(session, new Envelope("UpdateResourcesResponse", new UpdateResourcesResponse(req.ResourceType, newBal)), ct);
    }
}
