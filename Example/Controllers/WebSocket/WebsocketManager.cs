using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Example.Models.WebSocket;
using Example.Services.Interfaces;

namespace Example.Controllers.WebSocket;

public class WebSocketManager : IWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _sockets = new();
    private readonly IMessageSerializationService _serializationService;
    private readonly ILogger<WebSocketManager> _logger;
    
    public WebSocketManager(
        IMessageSerializationService serializationService,
        ILogger<WebSocketManager> logger)
    {
        _serializationService = serializationService;
        _logger = logger;
    }
    
    public Task<WebSocketConnection> AddSocketAsync(System.Net.WebSockets.WebSocket socket, string? clientId = null, string? clientType = null)
    {
        var connection = new WebSocketConnection
        {
            Socket = socket,
            ClientId = clientId,
            ClientType = clientType
        };
        
        _sockets.TryAdd(connection.ConnectionId, connection);
        _logger.LogInformation($"Socket added: {connection.ConnectionId}, Client: {clientId ?? "anonymous"}");
        
        return Task.FromResult(connection);
    }
    
    public Task RemoveSocketAsync(string connectionId)
    {
        if (_sockets.TryRemove(connectionId, out var connection))
        {
            _logger.LogInformation($"Socket removed: {connection.ConnectionId}");
        }
        
        return Task.CompletedTask;
    }
    
    public async Task SendToSocketAsync(string connectionId, object data)
    {
        if (_sockets.TryGetValue(connectionId, out var connection))
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                var serialized = _serializationService.Serialize(data);
                var buffer = Encoding.UTF8.GetBytes(serialized);
                await connection.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            else
            {
                _logger.LogWarning($"Attempted to send to a non-open socket: {connectionId}");
            }
        }
        else
        {
            _logger.LogWarning($"Attempted to send to a non-existent socket: {connectionId}");
        }
    }
    
    public async Task SendToAllAsync(object data)
    {
        var serialized = _serializationService.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var tasks = _sockets.Values
            .Where(s => s.Socket.State == WebSocketState.Open)
            .Select(s => s.Socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None));
                
        await Task.WhenAll(tasks);
    }
    
    public async Task SendToIssuerAsync(string issuer, object data)
    {
        var serialized = _serializationService.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var tasks = _sockets.Values
            .Where(s => s.Socket.State == WebSocketState.Open && 
                   s.ClientType == issuer)
            .Select(s => s.Socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None));
                
        await Task.WhenAll(tasks);
    }
    
    public IEnumerable<WebSocketConnection> GetAllSockets()
    {
        return _sockets.Values;
    }
}
