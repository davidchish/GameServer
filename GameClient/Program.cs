using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GameShared;
using Serilog;



Log.Logger = new LoggerConfiguration()
   .MinimumLevel.Information()
   .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
   .CreateLogger();

Console.WriteLine("Connecting to ws://localhost:8080/ws ...");
using var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri("ws://localhost:8080/ws"), CancellationToken.None);
Log.Information("Connected.");

_ = Task.Run(async () => {
    var buf = new byte[64 * 1024];
    while (ws.State == WebSocketState.Open)
    {
        var res = await ws.ReceiveAsync(buf, CancellationToken.None);
        if (res.MessageType == WebSocketMessageType.Close) break;
        var json = Encoding.UTF8.GetString(buf, 0, res.Count);
        Log.Information("<< {Json}", json);
    }
});

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1) Login");
    Console.WriteLine("2) Update Resources");
    Console.WriteLine("3) Send Gift");
    Console.WriteLine("q) Quit");
    Console.Write("> ");
    var choice = Console.ReadLine();

    if (choice == "q") break;

    switch (choice)
    {
        case "1":
            Console.Write("DeviceId: ");
            var did = Console.ReadLine() ?? "";
            await SendAsync(ws, new Envelope("Login", new LoginRequest(did)));
            break;
        case "2":
            Console.Write("ResourceType (coins/rolls): ");
            var t = Console.ReadLine() ?? "coins";
            Console.Write("Value (+/- int): ");
            var v = int.Parse(Console.ReadLine() ?? "1");
            var rt = t == "rolls" ? ResourceType.rolls : ResourceType.coins;
            await SendAsync(ws, new Envelope("UpdateResources", new UpdateResourcesRequest(rt, v)));
            break;
        case "3":
            Console.Write("Friend PlayerId (GUID): ");
            var f = Guid.Parse(Console.ReadLine() ?? Guid.Empty.ToString());
            Console.Write("ResourceType (coins/rolls): ");
            var gt = Console.ReadLine() ?? "coins";
            Console.Write("Value (+int): ");
            var gv = int.Parse(Console.ReadLine() ?? "1");
            var grt = gt == "rolls" ? ResourceType.rolls : ResourceType.coins;
            await SendAsync(ws, new Envelope("SendGift", new SendGiftRequest(f, grt, gv)));
            break;
    }
}

try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }

static async Task SendAsync(ClientWebSocket ws, Envelope env)
{
    var json = JsonSerializer.Serialize(env);
    Log.Information(">> {Json}", json);
    var bytes = Encoding.UTF8.GetBytes(json);
    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}

