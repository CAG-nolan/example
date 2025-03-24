using System;
using System.Text.Json;
using Example.Models.WebSocket;

namespace Example.Services.WebSocket;

public static class WebSocketResponseBuilder
{
    public static WebSocketMessage CreateResponse(string originalMessageId, string type, object data)
    {
        return new WebSocketMessage
        {
            Id = Guid.NewGuid().ToString(),
            Issuer = IssuerType.Server,
            Type = type,
            Data = JsonSerializer.SerializeToElement(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Timestamp = DateTime.UtcNow
        };
    }
    
    public static WebSocketMessage CreateErrorResponse(string originalMessageId, string errorMessage)
    {
        var error = new { Message = errorMessage };
        
        return new WebSocketMessage
        {
            Id = Guid.NewGuid().ToString(),
            Issuer = IssuerType.Server,
            Type = MessageType.Error,
            Data = JsonSerializer.SerializeToElement(error, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Timestamp = DateTime.UtcNow
        };
    }
    
    public static WebSocketMessage CreateSuccessResponse(string originalMessageId, object? data = null)
    {
        return new WebSocketMessage
        {
            Id = Guid.NewGuid().ToString(),
            Issuer = IssuerType.Server,
            Type = MessageType.Success,
            Data = data != null 
                ? JsonSerializer.SerializeToElement(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : JsonSerializer.SerializeToElement(new { }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Timestamp = DateTime.UtcNow
        };
    }
}