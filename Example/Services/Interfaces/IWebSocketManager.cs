using Example.Models.WebSocket;

namespace Example.Services.Interfaces;

public interface IWebSocketManager
{
    Task<WebSocketConnection> AddSocketAsync(System.Net.WebSockets.WebSocket socket, string? clientId = null, string? clientType = null);
    Task RemoveSocketAsync(string connectionId);
    Task SendToSocketAsync(string connectionId, object data);
    Task SendToAllAsync(object data);
    Task SendToIssuerAsync(string issuer, object data);
    IEnumerable<WebSocketConnection> GetAllSockets();
}
