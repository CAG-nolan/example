using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Example.Models.Events;
using Example.Models.WebSocket;
using Example.Services.Interfaces;
using Example.Services.WebSocket;

namespace Example.Services.Handlers.Message;

public class MessageCreateHandler : IMessageHandler
{
    private readonly IMessageSerializationService _serializationService;
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    private readonly IWebSocketManager _webSocketManager;
    private readonly ILogger<MessageCreateHandler> _logger;
    
    public string MessageType => Models.WebSocket.MessageType.MessageCreate;
    
    public MessageCreateHandler(
        IMessageSerializationService serializationService,
        IEventService eventService,
        IRelayService relayService,
        IWebSocketManager webSocketManager,
        ILogger<MessageCreateHandler> logger)
    {
        _serializationService = serializationService;
        _eventService = eventService;
        _relayService = relayService;
        _webSocketManager = webSocketManager;
        _logger = logger;
    }
    
    public async Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection)
    {
        try
        {
            // Deserialize the specific data for this message type
            var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
            if (messageData == null)
            {
                _logger.LogWarning("Invalid message data format");
                await SendErrorResponse(connection, message.Id, "Invalid message data format");
                return;
            }
            
            // Set event base properties from the WebSocket message
            messageData.EventType = MessageType;
            messageData.Timestamp = message.Timestamp;
            messageData.ServerId = messageData.ServerId ?? "";
            messageData.ChannelId = messageData.ChannelId ?? "";
            messageData.UserId = messageData.UserId ?? "";
            messageData.CreatedAt = DateTime.UtcNow;
            messageData.UpdatedAt = DateTime.UtcNow;
            
            // Process and store the event
            var eventId = await _eventService.ProcessEvent(messageData);
            _logger.LogInformation($"Created message event with ID: {eventId}");
            
            // Relay to other connected clients
            await _relayService.RelayEvent(messageData);
            
            // Send success response
            await SendSuccessResponse(connection, message.Id, new { EventId = eventId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message create event");
            await SendErrorResponse(connection, message.Id, "Error processing message create event");
        }
    }
    
    private async Task SendErrorResponse(WebSocketConnection connection, string messageId, string errorMessage)
    {
        var response = WebSocketResponseBuilder.CreateErrorResponse(messageId, errorMessage);
        await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
    }
    
    private async Task SendSuccessResponse(WebSocketConnection connection, string messageId, object data)
    {
        var response = WebSocketResponseBuilder.CreateSuccessResponse(messageId, data);
        await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
    }
}
