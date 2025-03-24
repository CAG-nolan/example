using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Example.Models.Configuration;
using Example.Models.Events;
using Example.Models.WebSocket;
using Example.Services.Interfaces;
using Example.Services.WebSocket;

namespace Example.Services;

public class RelayService : IRelayService
{
    private readonly IWebSocketManager _webSocketManager;
    private readonly IMessageSerializationService _serializationService;
    private readonly HttpClient _httpClient;
    private readonly RelayConfig _config;
    private readonly ILogger<RelayService> _logger;
    
    public RelayService(
        IWebSocketManager webSocketManager,
        IMessageSerializationService serializationService,
        HttpClient httpClient,
        IOptions<RelayConfig> config,
        ILogger<RelayService> logger)
    {
        _webSocketManager = webSocketManager;
        _serializationService = serializationService;
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }
    
    public async Task RelayEvent(BaseEvent eventData)
    {
        // Create a WebSocket message from the event
        var message = new WebSocketMessage
        {
            Id = Guid.NewGuid().ToString(),
            Issuer = IssuerType.Server,
            Type = eventData.EventType,
            Data = JsonSerializer.SerializeToElement(eventData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Timestamp = DateTime.UtcNow
        };
        
        // Send to all connected WebSocket clients
        if (_config.WebSocketRelay)
        {
            await _webSocketManager.SendToAllAsync(message);
            _logger.LogInformation($"Event relayed via WebSocket: Type={eventData.EventType}, ID={eventData.Id}");
        }
        
        if (_config.HttpRelay && !string.IsNullOrEmpty(_config.HttpRelayEndpoint))
        {
            try
            {
                var json = _serializationService.Serialize(message);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_config.HttpRelayEndpoint, content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Event relayed via HTTP: Type={eventData.EventType}, ID={eventData.Id}");
                }
                else
                {
                    _logger.LogWarning($"Failed to relay event via HTTP: Status={response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error relaying event via HTTP");
            }
        }
    }
}
