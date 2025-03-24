using System.Net.WebSockets;
using System.Text;
using Example.Models.WebSocket;
using Example.Services.Interfaces;
using Example.Services.WebSocket;
using Example.Services.Interfaces;
using Example.Services.WebSocket;
using System.Net.WebSockets;

namespace Example.Controllers.WebSocket;

public class WebSocketController
{
    private readonly IWebSocketManager _webSocketManager;
    private readonly IMessageHandlerRegistry _messageHandlerRegistry;
    private readonly IMessageSerializationService _serializationService;
    private readonly ILogger<WebSocketController> _logger;
    
    public WebSocketController(
        IWebSocketManager webSocketManager,
        IMessageHandlerRegistry messageHandlerRegistry,
        IMessageSerializationService serializationService,
        ILogger<WebSocketController> logger)
    {
        _webSocketManager = webSocketManager;
        _messageHandlerRegistry = messageHandlerRegistry;
        _serializationService = serializationService;
        _logger = logger;
    }
    
    public async Task HandleConnection(System.Net.WebSockets.WebSocket socket, HttpContext context)
    {
        // Extract client info from headers or query string
        string? clientId = context.Request.Query["clientId"];
        string? clientType = context.Request.Query["clientType"];
        
        // Store connection in manager
        var connection = await _webSocketManager.AddSocketAsync(socket, clientId, clientType);
        
        // Process messages in a loop
        var buffer = new byte[4096];
        WebSocketReceiveResult? result = null;
        
        try
        {
            do
            {
                using var ms = new MemoryStream();
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.Count > 0)
                    {
                        ms.Write(buffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync();
                    await ProcessMessage(message, connection);
                }
            }
            while (socket.State == WebSocketState.Open && result?.MessageType != WebSocketMessageType.Close);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection");
        }
        finally
        {
            await _webSocketManager.RemoveSocketAsync(connection.ConnectionId);
            
            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing WebSocket connection");
                }
            }
        }
    }
    
    private async Task ProcessMessage(string message, WebSocketConnection connection)
    {
        try
        {
            var wsMessage = _serializationService.DeserializeMessage(message);
            if (wsMessage == null)
            {
                _logger.LogWarning("Received invalid message format");
                await SendErrorResponse(connection, "Invalid message format");
                return;
            }
            
            _logger.LogInformation($"Received message: Type={wsMessage.Type}, Id={wsMessage.Id}, Issuer={wsMessage.Issuer}");
            
            if (_messageHandlerRegistry.HasHandler(wsMessage.Type))
            {
                var handler = _messageHandlerRegistry.GetHandler(wsMessage.Type);
                await handler.HandleMessageAsync(wsMessage, connection);
            }
            else
            {
                _logger.LogWarning($"No handler registered for message type: {wsMessage.Type}");
                await SendErrorResponse(connection, $"Unknown message type: {wsMessage.Type}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket message");
            await SendErrorResponse(connection, "Internal server error");
        }
    }
    
    private async Task SendErrorResponse(WebSocketConnection connection, string errorMessage)
    {
        var response = WebSocketResponseBuilder.CreateErrorResponse("unknown", errorMessage);
        await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
    }
}
