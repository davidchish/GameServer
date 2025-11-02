using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using GameShared;
using GameServer.Messaging;

namespace GameServer.WebSocketing;

public sealed class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly string _wsPath;

    public WebSocketServer(string wsUrl)
    {
        var uri = new Uri(wsUrl);
        _wsPath = uri.AbsolutePath.TrimEnd('/');
        _listener = new HttpListener();
        var prefix = $"{uri.Scheme}://localhost:{uri.Port}/";
        _listener.Prefixes.Add(prefix);
    }

    public async Task StartAsync(MessageRouter router, CancellationToken ct = default)
    {
        _listener.Start();
        Log.Information("Listening on ws://localhost:{Port}{Path}", new Uri(_listener.Prefixes.First()).Port, _wsPath);

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleContextAsync(ctx, router, ct), ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Log.Warning(ex, "Accept error");
                if (ctx is not null)
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.Close();
                }
            }
        }

        _listener.Stop();
        _listener.Close();
    }

    private async Task HandleContextAsync(HttpListenerContext context, MessageRouter router, CancellationToken ct)
    {
        if (!context.Request.IsWebSocketRequest || context.Request.Url?.AbsolutePath.TrimEnd('/') != _wsPath)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        WebSocket? ws = null;
        try
        {
            var wsCtx = await context.AcceptWebSocketAsync(null);
            ws = wsCtx.WebSocket;
            var session = new Session(ws);
            Log.Information("Client connected: {SessionId}", session.SessionId);
            await ReceiveLoopAsync(session, router, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HandleContext error");
            if (ws is not null)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "error", CancellationToken.None); } catch {}
            }
        }
    }

    private static async Task ReceiveLoopAsync(Session session, MessageRouter router, CancellationToken ct)
    {
        var ws = session.Socket;
        var buffer = new byte[64 * 1024];

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException) { break; }

            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text) continue;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("Type").GetString();
                var payload = doc.RootElement.GetProperty("Payload").GetRawText();
                if (string.IsNullOrWhiteSpace(type))
                {
                    await SendAsync(session, new Envelope("Error", new ErrorResponse("MissingType")), ct);
                    continue;
                }
                await router.DispatchAsync(session, type!, payload, ct);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Bad message");
                await SendAsync(session, new Envelope("Error", new ErrorResponse("BadRequest", ex.Message)), ct);
            }
        }

        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch {}
        Log.Information("Client disconnected: {SessionId}", session.SessionId);
    }

    public static async Task SendAsync(Session session, Envelope env, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(env);
        var bytes = Encoding.UTF8.GetBytes(json);
        await session.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }
}
