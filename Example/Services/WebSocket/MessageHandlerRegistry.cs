using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Example.Services.Interfaces;

namespace Example.Services.WebSocket;

public class MessageHandlerRegistry : IMessageHandlerRegistry
{
    private readonly Dictionary<string, IMessageHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<MessageHandlerRegistry> _logger;
    
    public MessageHandlerRegistry(ILogger<MessageHandlerRegistry> logger)
    {
        _logger = logger;
    }
    
    public void RegisterHandler(IMessageHandler handler)
    {
        _handlers[handler.MessageType] = handler;
        _logger.LogInformation($"Registered handler for message type: {handler.MessageType}");
    }
    
    public IMessageHandler GetHandler(string messageType)
    {
        if (!_handlers.TryGetValue(messageType, out var handler))
        {
            throw new KeyNotFoundException($"No handler registered for message type: {messageType}");
        }
        return handler;
    }
    
    public bool HasHandler(string messageType)
    {
        return _handlers.ContainsKey(messageType);
    }
}
