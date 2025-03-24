
// WebSocketEventRelay/Services/WebSocket/MessageSerializationService.cs
using System.Text.Json;
using Example.Models.WebSocket;
using Example.Services.Interfaces;

namespace Example.Services.WebSocket;

public class MessageSerializationService : IMessageSerializationService
{
    private readonly JsonSerializerOptions _options;
    
    public MessageSerializationService()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    public string Serialize<T>(T message)
    {
        return JsonSerializer.Serialize(message, _options);
    }
    
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }
    
    public WebSocketMessage? DeserializeMessage(string json)
    {
        return Deserialize<WebSocketMessage>(json);
    }
    
    public TData? DeserializeData<TData>(JsonElement dataElement)
    {
        return JsonSerializer.Deserialize<TData>(dataElement.GetRawText(), _options);
    }
}
