namespace Example.Services.Interfaces;

using System.Threading.Tasks;
using Example.Models.WebSocket;

public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection);
}
