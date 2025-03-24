namespace Example.Models.Configuration;

public class RelayConfig
{
    public bool WebSocketRelay { get; set; } = true;
    public bool HttpRelay { get; set; } = false;
    public string? HttpRelayEndpoint { get; set; }
}
