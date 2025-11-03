using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Enrichers;             
using Serilog.Context;
using GameServer.Messaging;
using GameServer.WebSocketing;
using GameServer.Domain;
using GameServer.Messaging.Handlers;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | {Properties}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Bootstrapping container...");
    var services = new ServiceCollection();

    // Domain
    services.AddSingleton<PlayerManager>();
    services.AddSingleton<SessionManager>();

    // Handlers
    services.AddSingleton<IMessageHandler, LoginHandler>();
    services.AddSingleton<IMessageHandler, UpdateResourcesHandler>();
    services.AddSingleton<IMessageHandler, SendGiftHandler>();

    // Router & server
    services.AddSingleton<MessageRouter>();
    services.AddSingleton<WebSocketServer>(sp => new WebSocketServer(
        "http://localhost:8080/ws/",
        sp.GetRequiredService<PlayerManager>(),
        sp.GetRequiredService<SessionManager>()));

    var provider = services.BuildServiceProvider();

    var server = provider.GetRequiredService<WebSocketServer>();
    var router = provider.GetRequiredService<MessageRouter>();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    Log.Information("Starting GameServer (Ctrl+C to stop)...");
    using (LogContext.PushProperty("Svc", "Server"))
    {
        await server.StartAsync(router, cts.Token);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error");
}
finally
{
    Log.CloseAndFlush();
}
