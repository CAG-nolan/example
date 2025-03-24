using System.Text.Json;
using System.Text.Json.Serialization;

namespace Example.Models.WebSocket;

public class WebSocketMessage
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

public static class IssuerType
{
    public const string Bot = "BOT";
    public const string Server = "SERVER";
    public const string Dashboard = "DASHBOARD";
}

public static class MessageType
{
    // Message events
    public const string MessageCreate = "MESSAGE_CREATE";
    public const string MessageUpdate = "MESSAGE_UPDATE";
    public const string MessageDelete = "MESSAGE_DELETE";
        
    // Voice events
    public const string VoiceJoin = "VOICE_JOIN";
    public const string VoiceLeave = "VOICE_LEAVE";
        
    // User events
    public const string UserUpdate = "USER_UPDATE";
    public const string UserJoin = "USER_JOIN";
        
    // Guild events
    public const string GuildUpdate = "GUILD_UPDATE";
        
    // Response types
    public const string Success = "SUCCESS";
    public const string Error = "ERROR";
    
    // TODO: Add more MessageTypes
}