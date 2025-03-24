namespace Example.Services;

// WebSocketEventRelay/Services/EventService.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Example.Models.Events;
using Example.Repositories.Interfaces;
using Example.Services.Interfaces;


public class EventService : IEventService
{
    private readonly IRepository<BaseEvent> _repository;
    private readonly ILogger<EventService> _logger;
    
    public EventService(IRepository<BaseEvent> repository, ILogger<EventService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<int> ProcessEvent(BaseEvent eventData)
    {
        if (string.IsNullOrEmpty(eventData.EventType))
        {
            throw new ArgumentException("Event type cannot be empty");
        }
        
        // Set timestamps if not already set
        eventData.Timestamp = eventData.Timestamp == default ? DateTime.UtcNow : eventData.Timestamp;
        eventData.CreatedAt = DateTime.UtcNow;
        eventData.UpdatedAt = DateTime.UtcNow;
        
        // Save event
        var id = await _repository.Create(eventData);
        _logger.LogInformation($"Processed event: Type={eventData.EventType}, ID={id}");
        
        return id;
    }
}
