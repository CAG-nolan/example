namespace Example.Models.WebSocket;

using System;
using System.Collections.Generic;
using System.Net.WebSockets;

public class WebSocketConnection
{
    public string ConnectionId { get; set; } = Guid.NewGuid().ToString();
    public WebSocket Socket { get; set; } = null!;
    public string? ClientId { get; set; }
    public string? ClientType { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
