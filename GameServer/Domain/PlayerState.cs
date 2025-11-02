namespace GameServer.Domain;

public sealed class PlayerState
{
    public Guid PlayerId { get; init; }
    public string DeviceId { get; init; } = default!;
    public int Coins { get; set; } = 100;
    public int Rolls { get; set; } = 10;
}
