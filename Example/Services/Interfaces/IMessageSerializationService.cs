using System.Text.Json;
using Example.Models.WebSocket;

namespace Example.Services.Interfaces;

public interface IMessageSerializationService
{
    string Serialize<T>(T message);
    T? Deserialize<T>(string json);
    WebSocketMessage? DeserializeMessage(string json);
    TData? DeserializeData<TData>(JsonElement dataElement);
}
