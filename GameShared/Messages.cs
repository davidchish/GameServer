namespace GameShared;

public enum ResourceType { coins, rolls }

public record Envelope(string Type, object Payload, int Version = 1);

public record ErrorResponse(string Error, string? Details = null);

public record LoginRequest(string DeviceId);
public record LoginResponse(Guid PlayerId, string Message);

public record UpdateResourcesRequest(ResourceType ResourceType, int ResourceValue);
public record UpdateResourcesResponse(ResourceType ResourceType, int NewBalance);

public record SendGiftRequest(Guid FriendPlayerId, ResourceType ResourceType, int ResourceValue);
public record GiftEvent(Guid FromPlayerId, ResourceType ResourceType, int ResourceValue, DateTime SentAtUtc);
