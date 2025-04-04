CAG-nolan: You are tasked with designing a websocket server that acts as a relay and persistent storage mechanism between two services. You must accept messages from the client, save them, and relay them to anotehr service in the same format. The strucutre is:
```
// Use this at dbdiagram.io to visualize the relationships

Table BaseEvent {
  Id int [pk, increment]
  EventType varchar
  Timestamp datetime
  ServerId varchar
  ChannelId varchar
  UserId varchar
  RawData text
  CreatedAt datetime
  UpdatedAt datetime
}

Table MessageEvent {
  Id int [pk, increment] 
  BaseEventId int [ref: > BaseEvent.Id]
  MessageId varchar
  Content text
  Mentions text
  Attachments text
  IsEdited bool
  IsDeleted bool
}

Table CommandEvent {
  Id int [pk, increment]
  BaseEventId int [ref: > BaseEvent.Id]
  CommandName varchar
  Arguments text
  IsSuccess bool
  ErrorMessage text
  ExecutionTimeMs int
}

Table ReactionEvent {
  Id int [pk, increment]
  BaseEventId int [ref: > BaseEvent.Id]
  MessageId varchar
  EmojiName varchar
  EmojiId varchar
  IsAdded bool
}

Table UserEvent {
  Id int [pk, increment]
  BaseEventId int [ref: > BaseEvent.Id]
  EventName varchar // Join, Leave, Ban, etc.
  AdditionalInfo text
}

Table VoiceEvent {
  Id int [pk, increment]
  BaseEventId int [ref: > BaseEvent.Id]
  EventName varchar // Join, Leave, Mute, etc.
  VoiceChannelId varchar
  DurationSeconds int
}

Table MetricEvent {
  Id int [pk, increment]
  BaseEventId int [ref: > BaseEvent.Id]
  MetricName varchar
  MetricValue float
  Tags text
}
```

you must follow SOLID principals, use websockets AND http (real-time vs historical data), and use a controller-service-repository pattern. How might you do this? 

GitHub Copilot: # WebSocket Event Relay Server Design

I'll design a WebSocket server that acts as a relay and persistent storage mechanism between two services, following SOLID principles and using the controller-service-repository pattern.

## Architecture Overview

```
┌───────────┐     ┌─────────────────────┐     ┌───────────┐
│ Client    │◄────┤ WebSocket Server    │────►│ Target    │
│ Service   │     │ (Relay + Storage)   │     │ Service   │
└───────────┘     └─────────────────────┘     └───────────┘
                           │
                           ▼
                  ┌─────────────────┐
                  │ Database        │
                  └─────────────────┘
```

## Components

### Controllers

1. **WebSocketController**: Handles real-time event streaming
2. **EventApiController**: Provides HTTP endpoints for historical data

```csharp
// WebSocketController.cs
public class WebSocketController
{
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    
    public WebSocketController(IEventService eventService, IRelayService relayService)
    {
        _eventService = eventService;
        _relayService = relayService;
    }
    
    public async Task HandleConnection(WebSocket socket)
    {
        // Authentication logic here
        
        await ProcessMessages(socket);
    }
    
    private async Task ProcessMessages(WebSocket socket)
    {
        // Process incoming WebSocket messages
        // Save to storage via _eventService
        // Relay to destination via _relayService
    }
}
```

```csharp
// EventApiController.cs
[ApiController]
[Route("api/events")]
public class EventApiController : ControllerBase
{
    private readonly IQueryService _queryService;
    
    public EventApiController(IQueryService queryService)
    {
        _queryService = queryService;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] EventQueryParameters parameters)
    {
        var events = await _queryService.GetEvents(parameters);
        return Ok(events);
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetEventById(int id)
    {
        var eventData = await _queryService.GetEventById(id);
        return eventData != null ? Ok(eventData) : NotFound();
    }
    
    // Additional endpoints for specific event types
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessageEvents([FromQuery] MessageQueryParameters parameters)
    {
        var events = await _queryService.GetMessageEvents(parameters);
        return Ok(events);
    }
}
```

### Services

```csharp
// IEventService.cs - Process and validate incoming events
public interface IEventService
{
    Task<int> ProcessEvent(BaseEvent eventData);
}

// IRelayService.cs - Forward events to destination
public interface IRelayService
{
    Task RelayEvent(BaseEvent eventData);
}

// IQueryService.cs - Retrieve historical events
public interface IQueryService
{
    Task<BaseEvent> GetEventById(int id);
    Task<IEnumerable<BaseEvent>> GetEvents(EventQueryParameters parameters);
    Task<IEnumerable<MessageEvent>> GetMessageEvents(MessageQueryParameters parameters);
    // Other specialized query methods
}
```

### Event Service Implementation

```csharp
public class EventService : IEventService
{
    private readonly IEventRepository _repository;
    
    public EventService(IEventRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<int> ProcessEvent(BaseEvent eventData)
    {
        // Validate event structure
        ValidateEvent(eventData);
        
        // Set timestamps
        eventData.Timestamp = eventData.Timestamp == default ? DateTime.UtcNow : eventData.Timestamp;
        eventData.CreatedAt = DateTime.UtcNow;
        eventData.UpdatedAt = DateTime.UtcNow;
        
        // Save event
        return await _repository.SaveEvent(eventData);
    }
    
    private void ValidateEvent(BaseEvent eventData)
    {
        // Validation logic based on event type
    }
}
```

### Relay Service Implementation

```csharp
public class RelayService : IRelayService
{
    private readonly WebSocketManager _webSocketManager;
    private readonly HttpClient _httpClient;
    private readonly RelayConfig _config;
    
    public RelayService(
        WebSocketManager webSocketManager, 
        HttpClient httpClient,
        IOptions<RelayConfig> config)
    {
        _webSocketManager = webSocketManager;
        _httpClient = httpClient;
        _config = config.Value;
    }
    
    public async Task RelayEvent(BaseEvent eventData)
    {
        var json = JsonSerializer.Serialize(eventData);
        
        // Relay via WebSockets for real-time updates
        if (_config.UseWebSockets)
        {
            await _webSocketManager.SendToAllAsync(json);
        }
        
        // Also relay via HTTP if configured
        if (_config.UseHttpRelay)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(_config.RelayEndpoint, content);
        }
    }
}
```

### Repositories

```csharp
// IEventRepository.cs - Data access interface
public interface IEventRepository
{
    Task<int> SaveEvent(BaseEvent eventData);
    Task<BaseEvent> GetEventById(int id);
    Task<IEnumerable<BaseEvent>> GetEvents(EventQueryParameters parameters);
    Task<IEnumerable<MessageEvent>> GetMessageEvents(MessageQueryParameters parameters);
    // Other specialized methods for different event types
}
```

### Repository Implementation

```csharp
public class EventRepository : IEventRepository
{
    private readonly ApplicationDbContext _dbContext;
    
    public EventRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<int> SaveEvent(BaseEvent eventData)
    {
        // Save base event
        _dbContext.BaseEvents.Add(eventData);
        await _dbContext.SaveChangesAsync();
        
        // Handle specific event type
        switch (eventData.EventType)
        {
            case "Message":
                await SaveMessageEvent(eventData as MessageEvent);
                break;
            case "Command":
                await SaveCommandEvent(eventData as CommandEvent);
                break;
            // Additional event types...
        }
        
        return eventData.Id;
    }
    
    private async Task SaveMessageEvent(MessageEvent messageEvent)
    {
        if (messageEvent == null) return;
        
        messageEvent.BaseEventId = messageEvent.Id;
        _dbContext.MessageEvents.Add(messageEvent);
        await _dbContext.SaveChangesAsync();
    }
    
    // Implementation of other repository methods...
}
```

## Dependency Injection Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Database
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
    
    // Repositories
    services.AddScoped<IEventRepository, EventRepository>();
    
    // Services
    services.AddScoped<IEventService, EventService>();
    services.AddScoped<IRelayService, RelayService>();
    services.AddScoped<IQueryService, QueryService>();
    
    // Web infrastructure
    services.AddSingleton<WebSocketManager>();
    services.AddHttpClient();
    
    // Configuration
    services.Configure<RelayConfig>(Configuration.GetSection("RelayConfig"));
    
    services.AddControllers();
}
```

## WebSocket Middleware

```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var controller = context.RequestServices.GetRequiredService<WebSocketController>();
            await controller.HandleConnection(socket);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});
```

## Key Features

- **Separation of Concerns**: Controllers, services, and repositories each have distinct responsibilities
- **Real-time + Historical**: WebSockets for real-time relay, HTTP API for historical data
- **SOLID Principles**:
  - Single Responsibility: Each class has one job
  - Open/Closed: Extensible for new event types without modifying existing code
  - Liskov Substitution: Event subtypes can be used where base types are expected
  - Interface Segregation: Clean, purpose-specific interfaces
  - Dependency Inversion: High-level modules depend on abstractions

This design provides a scalable, maintainable system that fulfills all requirements while ensuring clean separation between components.

CAG-nolan: lets say it will expand to have many event types (message create/update/delete, user update/create/delete, guild updates, etc). How would you design your system to be capable of cleanly being architected to support these?

GitHub Copilot: # Expandable Event Architecture Design

To support a growing number of event types (messages, users, guilds, etc.), I'll design a more scalable architecture that allows adding new event types without modifying existing code.

## Event Type System

### Event Registration & Factory System

```csharp
// Core event type registry
public interface IEventTypeRegistry
{
    void RegisterEventType<TEvent>(string eventType) where TEvent : BaseEvent;
    Type GetEventType(string eventType);
    bool IsRegistered(string eventType);
}

public class EventTypeRegistry : IEventTypeRegistry
{
    private readonly Dictionary<string, Type> _eventTypes = new();

    public void RegisterEventType<TEvent>(string eventType) where TEvent : BaseEvent
    {
        _eventTypes[eventType] = typeof(TEvent);
    }

    public Type GetEventType(string eventType)
    {
        return _eventTypes.TryGetValue(eventType, out var type) ? type : typeof(BaseEvent);
    }

    public bool IsRegistered(string eventType) => _eventTypes.ContainsKey(eventType);
}
```

### Event Factory

```csharp
public interface IEventFactory
{
    BaseEvent CreateEvent(string eventType, JsonElement data);
}

public class EventFactory : IEventFactory
{
    private readonly IEventTypeRegistry _eventTypeRegistry;

    public EventFactory(IEventTypeRegistry eventTypeRegistry)
    {
        _eventTypeRegistry = eventTypeRegistry;
    }

    public BaseEvent CreateEvent(string eventType, JsonElement data)
    {
        var type = _eventTypeRegistry.GetEventType(eventType);
        return (BaseEvent)JsonSerializer.Deserialize(data.GetRawText(), type);
    }
}
```

## Event Processors (Strategy Pattern)

```csharp
// Event processor interface
public interface IEventProcessor
{
    string EventType { get; }
    Task ProcessEvent(BaseEvent baseEvent);
}

// Specific event processors
public class MessageCreateProcessor : IEventProcessor
{
    private readonly IMessageRepository _repository;
    
    public string EventType => "MessageCreate";
    
    public MessageCreateProcessor(IMessageRepository repository)
    {
        _repository = repository;
    }
    
    public async Task ProcessEvent(BaseEvent baseEvent)
    {
        var messageEvent = (MessageEvent)baseEvent;
        // Process message creation specifics
        await _repository.Create(messageEvent);
    }
}

public class MessageUpdateProcessor : IEventProcessor
{
    private readonly IMessageRepository _repository;
    
    public string EventType => "MessageUpdate";
    
    public MessageUpdateProcessor(IMessageRepository repository)
    {
        _repository = repository;
    }
    
    public async Task ProcessEvent(BaseEvent baseEvent)
    {
        var messageEvent = (MessageEvent)baseEvent;
        messageEvent.IsEdited = true;
        // Process message update specifics
        await _repository.Update(messageEvent);
    }
}

// Similar processors for other event types
```

## Event Processor Registry

```csharp
public interface IEventProcessorRegistry
{
    void RegisterProcessor(IEventProcessor processor);
    IEventProcessor GetProcessor(string eventType);
}

public class EventProcessorRegistry : IEventProcessorRegistry
{
    private readonly Dictionary<string, IEventProcessor> _processors = new();
    
    public void RegisterProcessor(IEventProcessor processor)
    {
        _processors[processor.EventType] = processor;
    }
    
    public IEventProcessor GetProcessor(string eventType)
    {
        return _processors.TryGetValue(eventType, out var processor) 
            ? processor 
            : throw new KeyNotFoundException($"No processor registered for event type: {eventType}");
    }
}
```

## Generic Repository Pattern

```csharp
// Generic repository interface
public interface IRepository<TEntity> where TEntity : class
{
    Task<int> Create(TEntity entity);
    Task<TEntity> GetById(int id);
    Task<IEnumerable<TEntity>> GetAll(Func<IQueryable<TEntity>, IQueryable<TEntity>> query = null);
    Task Update(TEntity entity);
    Task Delete(int id);
}

// Generic repository implementation
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly ApplicationDbContext _dbContext;
    protected readonly DbSet<TEntity> _dbSet;
    
    public Repository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
    }
    
    public async Task<int> Create(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
        
        // Assuming entity has an Id property
        return (int)entity.GetType().GetProperty("Id").GetValue(entity);
    }
    
    public async Task<TEntity> GetById(int id)
    {
        return await _dbSet.FindAsync(id);
    }
    
    public async Task<IEnumerable<TEntity>> GetAll(Func<IQueryable<TEntity>, IQueryable<TEntity>> query = null)
    {
        IQueryable<TEntity> queryable = _dbSet;
        
        if (query != null)
            queryable = query(queryable);
            
        return await queryable.ToListAsync();
    }
    
    public async Task Update(TEntity entity)
    {
        _dbContext.Entry(entity).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task Delete(int id)
    {
        var entity = await GetById(id);
        _dbSet.Remove(entity);
        await _dbContext.SaveChangesAsync();
    }
}
```

## Specialized Repositories

```csharp
// For Message events
public interface IMessageRepository : IRepository<MessageEvent>
{
    Task<IEnumerable<MessageEvent>> GetMessagesByChannel(string channelId, DateTime? since = null);
}

public class MessageRepository : Repository<MessageEvent>, IMessageRepository
{
    public MessageRepository(ApplicationDbContext dbContext) : base(dbContext) { }
    
    public async Task<IEnumerable<MessageEvent>> GetMessagesByChannel(string channelId, DateTime? since = null)
    {
        IQueryable<MessageEvent> query = _dbSet
            .Join(_dbContext.BaseEvents,
                msg => msg.BaseEventId,
                baseEvent => baseEvent.Id,
                (msg, baseEvent) => new { Message = msg, Base = baseEvent })
            .Where(joined => joined.Base.ChannelId == channelId)
            .Select(joined => joined.Message);
            
        if (since.HasValue)
        {
            query = query.Join(_dbContext.BaseEvents,
                msg => msg.BaseEventId,
                baseEvent => baseEvent.Id,
                (msg, baseEvent) => new { Message = msg, Base = baseEvent })
                .Where(joined => joined.Base.Timestamp >= since.Value)
                .Select(joined => joined.Message);
        }
        
        return await query.ToListAsync();
    }
}

// Similar repositories for other event types
```

## Enhanced Event Service

```csharp
public class EventService : IEventService
{
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IEventFactory _eventFactory;
    private readonly IEventProcessorRegistry _processorRegistry;
    private readonly IRepository<BaseEvent> _baseRepository;
    
    public EventService(
        IEventTypeRegistry eventTypeRegistry,
        IEventFactory eventFactory,
        IEventProcessorRegistry processorRegistry,
        IRepository<BaseEvent> baseRepository)
    {
        _eventTypeRegistry = eventTypeRegistry;
        _eventFactory = eventFactory;
        _processorRegistry = processorRegistry;
        _baseRepository = baseRepository;
    }
    
    public async Task<int> ProcessEvent(string eventType, JsonElement data)
    {
        // Create appropriate event instance
        var eventData = _eventFactory.CreateEvent(eventType, data);
        
        // Set common fields
        eventData.EventType = eventType;
        eventData.Timestamp = eventData.Timestamp == default ? DateTime.UtcNow : eventData.Timestamp;
        eventData.CreatedAt = DateTime.UtcNow;
        eventData.UpdatedAt = DateTime.UtcNow;
        
        // Save base event first
        var baseEventId = await _baseRepository.Create(eventData);
        eventData.Id = baseEventId;
        
        // Process with the appropriate handler
        var processor = _processorRegistry.GetProcessor(eventType);
        await processor.ProcessEvent(eventData);
        
        return baseEventId;
    }
}
```

## WebSocket Controller Improvement

```csharp
public class WebSocketController
{
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    private readonly ILogger<WebSocketController> _logger;
    
    public WebSocketController(
        IEventService eventService, 
        IRelayService relayService,
        ILogger<WebSocketController> logger)
    {
        _eventService = eventService;
        _relayService = relayService;
        _logger = logger;
    }
    
    public async Task HandleConnection(WebSocket socket)
    {
        // Authentication logic here
        
        await ProcessMessages(socket);
    }
    
    private async Task ProcessMessages(WebSocket socket)
    {
        var buffer = new byte[4096];
        var receiveResult = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
            
        while (!receiveResult.CloseStatus.HasValue)
        {
            try
            {
                var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.TryGetProperty("event_type", out var typeElement))
                {
                    var eventType = typeElement.GetString();
                    
                    // Process and store event
                    var eventId = await _eventService.ProcessEvent(eventType, root);
                    
                    // Relay to destination service
                    await _relayService.RelayEvent(eventType, root);
                }
                else
                {
                    _logger.LogWarning("Received message without event_type property");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message");
            }
            
            receiveResult = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        
        await socket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }
}
```

## Application Startup & Registration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Core registrations
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
    
    // Register event system components
    services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
    services.AddSingleton<IEventProcessorRegistry, EventProcessorRegistry>();
    services.AddSingleton<IEventFactory, EventFactory>();
    
    // Register repositories
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped<IMessageRepository, MessageRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IGuildRepository, GuildRepository>();
    // Add more repositories as needed
    
    // Register event processors
    services.AddScoped<IEventProcessor, MessageCreateProcessor>();
    services.AddScoped<IEventProcessor, MessageUpdateProcessor>();
    services.AddScoped<IEventProcessor, MessageDeleteProcessor>();
    services.AddScoped<IEventProcessor, UserCreateProcessor>();
    services.AddScoped<IEventProcessor, UserUpdateProcessor>();
    services.AddScoped<IEventProcessor, GuildUpdateProcessor>();
    // Add more processors as needed
    
    // Register services
    services.AddScoped<IEventService, EventService>();
    services.AddScoped<IRelayService, RelayService>();
    services.AddScoped<IQueryService, QueryService>();
    
    // Web infrastructure
    services.AddSingleton<WebSocketManager>();
    services.AddHttpClient();
    
    services.AddControllers();
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env, 
    IEventTypeRegistry eventTypeRegistry, IEventProcessorRegistry processorRegistry,
    IServiceProvider serviceProvider)
{
    // Register event types
    eventTypeRegistry.RegisterEventType<MessageEvent>("MessageCreate");
    eventTypeRegistry.RegisterEventType<MessageEvent>("MessageUpdate");
    eventTypeRegistry.RegisterEventType<MessageEvent>("MessageDelete");
    eventTypeRegistry.RegisterEventType<UserEvent>("UserCreate");
    eventTypeRegistry.RegisterEventType<UserEvent>("UserUpdate");
    eventTypeRegistry.RegisterEventType<UserEvent>("UserDelete");
    eventTypeRegistry.RegisterEventType<GuildEvent>("GuildUpdate");
    eventTypeRegistry.RegisterEventType<GuildEvent>("GuildCreate");
    // Register more event types as needed
    
    // Register processors from DI
    foreach (var processor in serviceProvider.GetServices<IEventProcessor>())
    {
        processorRegistry.RegisterProcessor(processor);
    }
    
    // Standard middleware setup
    app.UseWebSockets();
    // ...
}
```

## Key Benefits of This Design

1. **Open/Closed Principle**: Add new event types without modifying existing code
   - Register new event types in startup
   - Create new processors for new events
   - No change to core event handling logic

2. **Easy Extensibility**:
   - Adding a new event type requires:
     - A new event class that extends BaseEvent
     - A processor implementation
     - Registration in startup

3. **Type Safety**: Maintains strong typing throughout the system

4. **Loose Coupling**: Components interact through interfaces

5. **Domain-Specific Repositories**: Specialized repositories for each event type provide relevant query methods

6. **Testability**: All components depend on abstractions that can be mocked

7. **Separation of Concerns**: Clear responsibilities for:
   - Event creation (Factory)
   - Event registration (Registry)
   - Event processing (Processors)
   - Event storage (Repositories)

This architecture provides a clean, maintainable framework that can scale to handle dozens or hundreds of different event types without becoming unwieldy.

CAG-nolan: You reference a BaseEvent - what would this look like? Cna you provide the models for the event types?

GitHub Copilot: # Event Model Hierarchy

Here's a comprehensive model structure for the event system, starting with the `BaseEvent` and specialized event types.

## Base Event Model

```csharp
// BaseEvent.cs
public class BaseEvent
{
    public int Id { get; set; }
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string ServerId { get; set; }
    public string ChannelId { get; set; }
    public string UserId { get; set; }
    public string RawData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Additional metadata for system use
    [NotMapped]
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
```

## Message Events

```csharp
// MessageEvent.cs
public class MessageEvent : BaseEvent
{
    public int BaseEventId { get; set; }  // Foreign key to BaseEvent
    public string MessageId { get; set; }
    public string Content { get; set; }
    public string Mentions { get; set; }  // Serialized list of user IDs
    public string Attachments { get; set; }  // Serialized attachment data
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    
    // Helper methods for serialized collections
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(Mentions);
        
    public void SetMentionsList(List<string> mentions) => 
        Mentions = JsonSerializer.Serialize(mentions);
        
    public List<AttachmentInfo> GetAttachmentsList() =>
        string.IsNullOrEmpty(Attachments) ? new List<AttachmentInfo>() : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments);
        
    public void SetAttachmentsList(List<AttachmentInfo> attachments) =>
        Attachments = JsonSerializer.Serialize(attachments);
}

// Supporting model for attachments
public class AttachmentInfo
{
    public string Id { get; set; }
    public string Url { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; }
}
```

## Command Events

```csharp
// CommandEvent.cs
public class CommandEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string CommandName { get; set; }
    public string Arguments { get; set; }  // Serialized command arguments
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; }
    
    // Helper methods for arguments
    public Dictionary<string, object> GetArgumentsDict() =>
        string.IsNullOrEmpty(Arguments) ? new Dictionary<string, object>() : 
            JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments);
            
    public void SetArgumentsDict(Dictionary<string, object> args) =>
        Arguments = JsonSerializer.Serialize(args);
}
```

## Reaction Events

```csharp
// ReactionEvent.cs
public class ReactionEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string MessageId { get; set; }
    public string EmojiName { get; set; }
    public string EmojiId { get; set; }
    public bool IsAdded { get; set; }  // True if added, false if removed
}
```

## User Events

```csharp
// UserEvent.cs
public class UserEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Join, Leave, Ban, Kick, etc.
    public string AdditionalInfo { get; set; }  // Serialized additional info
    
    // Helper for additional info
    public Dictionary<string, object> GetAdditionalInfoDict() =>
        string.IsNullOrEmpty(AdditionalInfo) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(AdditionalInfo);
            
    public void SetAdditionalInfoDict(Dictionary<string, object> info) =>
        AdditionalInfo = JsonSerializer.Serialize(info);
}
```

## Voice Events

```csharp
// VoiceEvent.cs
public class VoiceEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Join, Leave, Mute, Deafen, etc.
    public string VoiceChannelId { get; set; }
    public int DurationSeconds { get; set; }  // Only applicable for certain events
}
```

## Metric Events

```csharp
// MetricEvent.cs
public class MetricEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string MetricName { get; set; }
    public float MetricValue { get; set; }
    public string Tags { get; set; }  // Serialized tags
    
    // Helper for tags
    public Dictionary<string, string> GetTagsDict() =>
        string.IsNullOrEmpty(Tags) ? new Dictionary<string, string>() :
            JsonSerializer.Deserialize<Dictionary<string, string>>(Tags);
            
    public void SetTagsDict(Dictionary<string, string> tags) =>
        Tags = JsonSerializer.Serialize(tags);
}
```

## Guild Events

```csharp
// GuildEvent.cs - For server/guild level events
public class GuildEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Create, Update, Delete, etc.
    public string GuildId { get; set; }
    public string Changes { get; set; }  // Serialized changes to guild properties
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(Changes);
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
}
```

## Channel Events

```csharp
// ChannelEvent.cs - For channel level events
public class ChannelEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Create, Update, Delete, etc.
    public string ChannelType { get; set; }  // Text, Voice, Category, etc.
    public string ParentId { get; set; }  // Parent category ID if applicable
    public string Changes { get; set; }  // Serialized changes
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(Changes);
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
}
```

## Role Events

```csharp
// RoleEvent.cs
public class RoleEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Create, Update, Delete, Assign, Remove
    public string RoleId { get; set; }
    public string TargetUserId { get; set; }  // For assign/remove events
    public string Changes { get; set; }  // For update events
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(Changes);
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
}
```

## DbContext Configuration

```csharp
// ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<BaseEvent> BaseEvents { get; set; }
    public DbSet<MessageEvent> MessageEvents { get; set; }
    public DbSet<CommandEvent> CommandEvents { get; set; }
    public DbSet<ReactionEvent> ReactionEvents { get; set; }
    public DbSet<UserEvent> UserEvents { get; set; }
    public DbSet<VoiceEvent> VoiceEvents { get; set; }
    public DbSet<MetricEvent> MetricEvents { get; set; }
    public DbSet<GuildEvent> GuildEvents { get; set; }
    public DbSet<ChannelEvent> ChannelEvents { get; set; }
    public DbSet<RoleEvent> RoleEvents { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships
        modelBuilder.Entity<MessageEvent>()
            .HasOne<BaseEvent>()
            .WithMany()
            .HasForeignKey(e => e.BaseEventId);
            
        modelBuilder.Entity<CommandEvent>()
            .HasOne<BaseEvent>()
            .WithMany()
            .HasForeignKey(e => e.BaseEventId);
            
        // Configure remaining relationships similarly
        
        // Configure serialized JSON properties
        modelBuilder.Entity<BaseEvent>()
            .Property(e => e.RawData)
            .HasColumnType("nvarchar(max)");
            
        modelBuilder.Entity<MessageEvent>()
            .Property(e => e.Mentions)
            .HasColumnType("nvarchar(max)");
            
        modelBuilder.Entity<MessageEvent>()
            .Property(e => e.Attachments)
            .HasColumnType("nvarchar(max)");
            
        // Configure remaining serialized columns similarly
    }
}
```

## Key Design Features

1. **Inheritance**: All event types extend from BaseEvent
2. **Foreign Key Relationships**: Each specialized event links to its base event
3. **JSON Serialization Helpers**: Convenient methods to work with serialized collections
4. **Clean Type Separation**: Each event type has its own class with relevant properties
5. **Expandability**: You can add new event types by creating new derived classes

This model structure provides a clean way to represent all your event types while maintaining the relational database schema.

When extending with new event subtypes (like MessageCreate vs MessageUpdate), you can:
1. Use the same event class with a different EventType string
2. Or subclass further if the structure differs significantly

This approach gives you both type safety and database efficiency while allowing for easy expansion of your event system.

CAG-nolan: Given your appliction design so far, can you create the filetree showing the dotnet structure? 

GitHub Copilot: # WebSocket Event Relay Server - File Structure

```
c:\Users\zng3030\Code\gbs_sop_ai
│
├── WebSocketEventRelay/
│   ├── WebSocketEventRelay.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Startup.cs
│   │
│   ├── Controllers/
│   │   ├── Api/
│   │   │   ├── EventApiController.cs
│   │   │   ├── MessageEventsController.cs
│   │   │   ├── UserEventsController.cs
│   │   │   ├── GuildEventsController.cs
│   │   │   ├── CommandEventsController.cs
│   │   │   └── MetricsController.cs
│   │   │
│   │   └── WebSocket/
│   │       ├── WebSocketController.cs
│   │       └── WebSocketManager.cs
│   │
│   ├── Models/
│   │   ├── Events/
│   │   │   ├── BaseEvent.cs
│   │   │   ├── MessageEvent.cs
│   │   │   ├── CommandEvent.cs
│   │   │   ├── ReactionEvent.cs
│   │   │   ├── UserEvent.cs
│   │   │   ├── VoiceEvent.cs
│   │   │   ├── MetricEvent.cs
│   │   │   ├── GuildEvent.cs
│   │   │   ├── ChannelEvent.cs
│   │   │   └── RoleEvent.cs
│   │   │
│   │   ├── Dto/
│   │   │   ├── EventQueryParameters.cs
│   │   │   ├── MessageQueryParameters.cs
│   │   │   ├── UserQueryParameters.cs
│   │   │   └── EventSummaryDto.cs
│   │   │
│   │   └── Supporting/
│   │       ├── AttachmentInfo.cs
│   │       └── RelayConfig.cs
│   │
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   ├── IEventService.cs
│   │   │   ├── IRelayService.cs
│   │   │   ├── IQueryService.cs
│   │   │   ├── IEventTypeRegistry.cs
│   │   │   ├── IEventFactory.cs
│   │   │   └── IEventProcessorRegistry.cs
│   │   │
│   │   ├── EventService.cs
│   │   ├── RelayService.cs
│   │   ├── QueryService.cs
│   │   ├── EventTypeRegistry.cs
│   │   ├── EventFactory.cs
│   │   └── EventProcessorRegistry.cs
│   │
│   ├── Processors/
│   │   ├── Interfaces/
│   │   │   └── IEventProcessor.cs
│   │   │
│   │   ├── Message/
│   │   │   ├── MessageCreateProcessor.cs
│   │   │   ├── MessageUpdateProcessor.cs
│   │   │   └── MessageDeleteProcessor.cs
│   │   │
│   │   ├── User/
│   │   │   ├── UserCreateProcessor.cs
│   │   │   ├── UserUpdateProcessor.cs
│   │   │   └── UserDeleteProcessor.cs
│   │   │
│   │   ├── Guild/
│   │   │   ├── GuildCreateProcessor.cs
│   │   │   ├── GuildUpdateProcessor.cs
│   │   │   └── GuildDeleteProcessor.cs
│   │   │
│   │   └── Reaction/
│   │       ├── ReactionAddProcessor.cs
│   │       └── ReactionRemoveProcessor.cs
│   │
│   ├── Repositories/
│   │   ├── Interfaces/
│   │   │   ├── IRepository.cs
│   │   │   ├── IMessageRepository.cs
│   │   │   ├── IUserRepository.cs
│   │   │   ├── IGuildRepository.cs
│   │   │   └── ICommandRepository.cs
│   │   │
│   │   ├── Repository.cs
│   │   ├── MessageRepository.cs
│   │   ├── UserRepository.cs
│   │   ├── GuildRepository.cs
│   │   └── CommandRepository.cs
│   │
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   ├── Migrations/
│   │   │   └── // Migration files will be generated here
│   │   │
│   │   └── Seed/
│   │       └── SeedData.cs
│   │
│   ├── Infrastructure/
│   │   ├── Middleware/
│   │   │   └── WebSocketMiddleware.cs
│   │   │
│   │   ├── Configuration/
│   │   │   └── EventRegistration.cs
│   │   │
│   │   └── Extensions/
│   │       ├── ServiceCollectionExtensions.cs
│   │       ├── ApplicationBuilderExtensions.cs
│   │       └── JsonExtensions.cs
│   │
│   └── Properties/
│       └── launchSettings.json
│
├── WebSocketEventRelay.Tests/
│   ├── WebSocketEventRelay.Tests.csproj
│   │
│   ├── Services/
│   │   ├── EventServiceTests.cs
│   │   ├── RelayServiceTests.cs
│   │   └── EventFactoryTests.cs
│   │
│   ├── Controllers/
│   │   ├── EventApiControllerTests.cs
│   │   └── WebSocketControllerTests.cs
│   │
│   ├── Processors/
│   │   ├── MessageProcessorTests.cs
│   │   └── UserProcessorTests.cs
│   │
│   ├── Repositories/
│   │   ├── MessageRepositoryTests.cs
│   │   └── UserRepositoryTests.cs
│   │
│   ├── Integration/
│   │   ├── WebSocketIntegrationTests.cs
│   │   └── ApiIntegrationTests.cs
│   │
│   └── TestHelpers/
│       ├── TestDbContext.cs
│       └── EventTestDataGenerator.cs
│
├── .gitignore
├── WebSocketEventRelay.sln
└── README.md
```

## Key Structure Elements

### Controllers

- **API Controllers**: REST endpoints for retrieving historical event data
- **WebSocket Controllers**: Handle real-time data streaming

### Models

- **Events**: Base event class and specialized derived classes
- **DTOs**: Data transfer objects for API requests/responses
- **Supporting**: Additional model classes that support the event types

### Services

- Core business logic classes:
  - `EventService`: Processes incoming events
  - `RelayService`: Forwards events to the target service
  - `QueryService`: Retrieves historical event data
  - `EventTypeRegistry`: Maintains mapping between event type strings and classes
  - `EventFactory`: Creates typed event instances from JSON

### Processors

- Strategy pattern implementations for different event types
- Organized by domain (Message, User, Guild, etc.)
- Each processor handles a specific event type

### Repositories

- Data access classes using the Repository pattern
- Generic base repository with specialized repositories for each event type

### Data

- Database context and migration support
- Seed data for development/testing

### Infrastructure

- Cross-cutting concerns:
  - Middleware for WebSocket handling
  - Configuration for event type registration
  - Extension methods for service setup and application configuration

### Tests Project

- Unit and integration tests for all components
- Organized to mirror the structure of the main project

This structure cleanly separates concerns while making the project organization intuitive and maintainable. It follows standard ASP.NET Core conventions while adding domain-specific organization for your event processing system.

CAG-nolan: Do the models need to change if im using Dotnet entity framework?

GitHub Copilot: # Entity Framework Compatible Event Models

Yes, the models need adjustments to work optimally with Entity Framework. Here's how the models should be structured for EF compatibility:

## Base Event Model

```csharp
// BaseEvent.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("BaseEvents")]
public class BaseEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    [MaxLength(50)]
    public string ServerId { get; set; }
    
    [MaxLength(50)]
    public string ChannelId { get; set; }
    
    [MaxLength(50)]
    public string UserId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string RawData { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; }
    
    [Required]
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties for EF relationships
    public virtual MessageEvent MessageEvent { get; set; }
    public virtual CommandEvent CommandEvent { get; set; }
    public virtual ReactionEvent ReactionEvent { get; set; }
    public virtual UserEvent UserEvent { get; set; }
    public virtual VoiceEvent VoiceEvent { get; set; }
    public virtual MetricEvent MetricEvent { get; set; }
    public virtual GuildEvent GuildEvent { get; set; }
    public virtual ChannelEvent ChannelEvent { get; set; }
    public virtual RoleEvent RoleEvent { get; set; }
    
    // Not mapped to database
    [NotMapped]
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
```

## Message Events

```csharp
// MessageEvent.cs
[Table("MessageEvents")]
public class MessageEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BaseEventId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Mentions { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Attachments { get; set; }
    
    public bool IsEdited { get; set; }
    
    public bool IsDeleted { get; set; }
    
    // Navigation property for EF relationship
    [ForeignKey("BaseEventId")]
    public virtual BaseEvent BaseEvent { get; set; }
    
    // Helper methods - not changed
    [NotMapped]
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(Mentions);
        
    public void SetMentionsList(List<string> mentions) => 
        Mentions = JsonSerializer.Serialize(mentions);
        
    [NotMapped]
    public List<AttachmentInfo> GetAttachmentsList() =>
        string.IsNullOrEmpty(Attachments) ? new List<AttachmentInfo>() : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments);
        
    public void SetAttachmentsList(List<AttachmentInfo> attachments) =>
        Attachments = JsonSerializer.Serialize(attachments);
}
```

## Command Events

```csharp
// CommandEvent.cs
[Table("CommandEvents")]
public class CommandEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BaseEventId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string CommandName { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Arguments { get; set; }
    
    public bool IsSuccess { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string ErrorMessage { get; set; }
    
    public int ExecutionTimeMs { get; set; }
    
    // Navigation property for EF relationship
    [ForeignKey("BaseEventId")]
    public virtual BaseEvent BaseEvent { get; set; }
    
    // Helper methods - not changed
    [NotMapped]
    public Dictionary<string, object> GetArgumentsDict() =>
        string.IsNullOrEmpty(Arguments) ? new Dictionary<string, object>() : 
            JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments);
            
    public void SetArgumentsDict(Dictionary<string, object> args) =>
        Arguments = JsonSerializer.Serialize(args);
}
```

## DbContext Configuration

```csharp
// ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<BaseEvent> BaseEvents { get; set; }
    public DbSet<MessageEvent> MessageEvents { get; set; }
    public DbSet<CommandEvent> CommandEvents { get; set; }
    public DbSet<ReactionEvent> ReactionEvents { get; set; }
    public DbSet<UserEvent> UserEvents { get; set; }
    public DbSet<VoiceEvent> VoiceEvents { get; set; }
    public DbSet<MetricEvent> MetricEvents { get; set; }
    public DbSet<GuildEvent> GuildEvents { get; set; }
    public DbSet<ChannelEvent> ChannelEvents { get; set; }
    public DbSet<RoleEvent> RoleEvents { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships (one-to-one)
        modelBuilder.Entity<BaseEvent>()
            .HasOne(b => b.MessageEvent)
            .WithOne(m => m.BaseEvent)
            .HasForeignKey<MessageEvent>(m => m.BaseEventId);
            
        modelBuilder.Entity<BaseEvent>()
            .HasOne(b => b.CommandEvent)
            .WithOne(c => c.BaseEvent)
            .HasForeignKey<CommandEvent>(c => c.BaseEventId);
            
        // Configure indexes for performance
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.EventType);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.ServerId);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.ChannelId);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.UserId);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.Timestamp);
            
        modelBuilder.Entity<MessageEvent>()
            .HasIndex(e => e.MessageId);
    }
}
```

## Key Changes for Entity Framework

1. **Data Annotations**:
   - Added `[Key]`, `[Required]`, `[MaxLength]`, `[Column]`, `[ForeignKey]`, and `[Table]` attributes
   - These specify constraints and mapping details for EF

2. **Navigation Properties**:
   - Added bidirectional navigation properties between parent/child entities
   - Marked with `virtual` for lazy loading support

3. **Column Type Definitions**:
   - Added explicit column types for text fields using `[Column(TypeName = "nvarchar(max)")]`

4. **Indexes**:
   - Added database indexes in `OnModelCreating()` for frequently queried fields
   - Improves query performance for common operations

5. **Relationship Configuration**:
   - Defined explicit one-to-one relationships in `OnModelCreating()`
   - Clarifies the relationship between base events and specific event types

The remaining event type classes would follow the same pattern, each with:
- Primary key
- Foreign key to BaseEvent
- Navigation property back to BaseEvent
- Data annotations for constraints
- Virtual keyword for lazy loading

These changes optimize the models for Entity Framework while maintaining the same logical structure and functionality. The models now explicitly define relationships that EF can use to generate efficient SQL and provide navigation between related entities.

CAG-nolan: Attribute 'System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute' is not valid on this declaration type. It is valid on 'Class, Property, Field' declarations only.

GitHub Copilot: # Entity Framework Models - Error Fix

You're right! The `[NotMapped]` attribute can only be applied to properties, fields, or classes - not to methods. Let me fix those errors in the code:

## Message Events Corrected

```csharp
// MessageEvent.cs
[Table("MessageEvents")]
public class MessageEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BaseEventId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Mentions { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Attachments { get; set; }
    
    public bool IsEdited { get; set; }
    
    public bool IsDeleted { get; set; }
    
    // Navigation property for EF relationship
    [ForeignKey("BaseEventId")]
    public virtual BaseEvent BaseEvent { get; set; }
    
    // Helper methods - without NotMapped attribute
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(Mentions);
        
    public void SetMentionsList(List<string> mentions) => 
        Mentions = JsonSerializer.Serialize(mentions);
        
    public List<AttachmentInfo> GetAttachmentsList() =>
        string.IsNullOrEmpty(Attachments) ? new List<AttachmentInfo>() : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments);
        
    public void SetAttachmentsList(List<AttachmentInfo> attachments) =>
        Attachments = JsonSerializer.Serialize(attachments);
    
    // Optional: Add NotMapped properties if you need them
    [NotMapped]
    public List<string> MentionsList 
    {
        get => GetMentionsList();
        set => SetMentionsList(value);
    }
    
    [NotMapped]
    public List<AttachmentInfo> AttachmentsList
    {
        get => GetAttachmentsList();
        set => SetAttachmentsList(value);
    }
}
```

## Command Events Corrected

```csharp
// CommandEvent.cs
[Table("CommandEvents")]
public class CommandEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BaseEventId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string CommandName { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Arguments { get; set; }
    
    public bool IsSuccess { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string ErrorMessage { get; set; }
    
    public int ExecutionTimeMs { get; set; }
    
    // Navigation property for EF relationship
    [ForeignKey("BaseEventId")]
    public virtual BaseEvent BaseEvent { get; set; }
    
    // Helper methods - without NotMapped attribute
    public Dictionary<string, object> GetArgumentsDict() =>
        string.IsNullOrEmpty(Arguments) ? new Dictionary<string, object>() : 
            JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments);
            
    public void SetArgumentsDict(Dictionary<string, object> args) =>
        Arguments = JsonSerializer.Serialize(args);
    
    // Optional: Add NotMapped properties if you need them
    [NotMapped]
    public Dictionary<string, object> ArgumentsDict
    {
        get => GetArgumentsDict();
        set => SetArgumentsDict(value);
    }
}
```

## BaseEvent Fix

For the `BaseEvent` class, the `Metadata` property is correctly marked with `[NotMapped]`:

```csharp
// BaseEvent.cs - relevant section only
public class BaseEvent
{
    // ... other properties ...
    
    // Not mapped to database - this is correct
    [NotMapped]
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
```

## Key Changes

1. **Removed `[NotMapped]` from methods** - The attribute was incorrectly placed on helper methods
2. **Added `[NotMapped]` properties** - Added proper properties that are decorated with the attribute
3. **Kept helper methods** - The methods remain but without the attribute

This approach:
- Fixes the compilation errors
- Keeps the helper methods for easy conversion
- Adds properly decorated properties that won't be mapped to the database
- Maintains clean Entity Framework integration

These changes ensure your models will work correctly with Entity Framework while providing convenient access to serialized data.

CAG-nolan: i get warnings like:
```
Non-nullable property 'BaseEvent' is uninitialized. Consider adding the 'required' modifier or declaring the property as nullable.
```
and that
```
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(Mentions);        
```
is a possible null reference. How do i get rid of these?

GitHub Copilot: # Entity Framework Models - Fixing Nullable Reference Warnings

Let's address both nullable reference warnings in the models:

## 1. Fix the Navigation Property Warning

```csharp
// MessageEvent.cs - Fix for "Non-nullable property 'BaseEvent' is uninitialized"
[Table("MessageEvents")]
public class MessageEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BaseEventId { get; set; }
    
    // Other properties...
    
    // Make the navigation property nullable
    [ForeignKey("BaseEventId")]
    public virtual BaseEvent? BaseEvent { get; set; }
    
    // Rest of the class...
}
```

Apply the same pattern to all classes with navigation properties:

```csharp
// CommandEvent.cs - navigation property fix
[ForeignKey("BaseEventId")]
public virtual BaseEvent? BaseEvent { get; set; }

// BaseEvent.cs - navigation properties fix
public virtual MessageEvent? MessageEvent { get; set; }
public virtual CommandEvent? CommandEvent { get; set; }
public virtual ReactionEvent? ReactionEvent { get; set; }
// etc. for other navigation properties
```

## 2. Fix the JSON Deserialization Warnings

```csharp
// MessageEvent.cs - Fix for JsonSerializer.Deserialize null possibility
public List<string> GetMentionsList() => 
    string.IsNullOrEmpty(Mentions) 
        ? new List<string>() 
        : JsonSerializer.Deserialize<List<string>>(Mentions) ?? new List<string>();
        
public void SetMentionsList(List<string> mentions) => 
    Mentions = JsonSerializer.Serialize(mentions);
        
public List<AttachmentInfo> GetAttachmentsList() =>
    string.IsNullOrEmpty(Attachments) 
        ? new List<AttachmentInfo>() 
        : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments) ?? new List<AttachmentInfo>();
```

Similarly, fix all other serialization methods:

```csharp
// CommandEvent.cs - Fix for JsonSerializer.Deserialize null possibility
public Dictionary<string, object> GetArgumentsDict() =>
    string.IsNullOrEmpty(Arguments) 
        ? new Dictionary<string, object>() 
        : JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments) ?? new Dictionary<string, object>();
```

## 3. Enable/Disable Nullable Reference Types (Optional)

You can also control these warnings at the project or file level:

### Project-level: To disable nullable reference types

In your .csproj file:
```xml
<PropertyGroup>
  <Nullable>disable</Nullable>
</PropertyGroup>
```

### File-level: To disable nullable reference warnings in specific files

At the top of the file:
```csharp
#nullable disable
// ... your code ...
#nullable restore
```

### Preferred Solution

The recommended approach is to properly handle nullability as shown in the fixes above, as it makes your code more robust. Adding the `?` to reference types explicitly shows that a property might be null, which helps prevent null reference exceptions at runtime.

## 4. Common Imports for the Models

Add these imports to your model files:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
```

These changes will eliminate both types of warnings while maintaining proper handling of potential null values.

CAG-nolan: can i still have the MessageEvent extend/implmenet the BaseEvent? or is that now handlered through the BaseEventId?

GitHub Copilot: # Entity Framework Inheritance vs. Foreign Key Approaches

You have two main options for relating `MessageEvent` to `BaseEvent`:

## Option 1: Inheritance (TPT - Table Per Type)

Yes, you can have `MessageEvent` inherit from `BaseEvent`. This is a common approach in object-oriented design and is supported by Entity Framework using Table-Per-Type inheritance.

```csharp
// BaseEvent.cs - Base class
[Table("BaseEvents")]
public class BaseEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    // Other base properties...
}

// MessageEvent.cs - Inherits from BaseEvent
[Table("MessageEvents")]
public class MessageEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; }
    
    // Other message-specific properties...
    
    // Helper methods
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(Mentions) ?? new List<string>();
}
```

With DbContext configuration:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Configure Table-Per-Type inheritance
    modelBuilder.Entity<BaseEvent>().ToTable("BaseEvents");
    modelBuilder.Entity<MessageEvent>().ToTable("MessageEvents");
    modelBuilder.Entity<CommandEvent>().ToTable("CommandEvents");
    // Configure other event types...
}
```

## Option 2: Foreign Key Relationship

The approach from my previous response uses foreign keys instead of inheritance:

```csharp
// BaseEvent.cs - Parent entity
[Table("BaseEvents")]
public class BaseEvent
{
    [Key]
    public int Id { get; set; }
    
    // Base properties...
    
    // Navigation properties
    public virtual MessageEvent? MessageEvent { get; set; }
    public virtual CommandEvent? CommandEvent { get; set; }
    // Other event references...
}

// MessageEvent.cs - Separate entity with FK relationship
[Table("MessageEvents")]
public class MessageEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int BaseEventId { get; set; }
    
    // Message-specific properties...
    
    // Navigation property back to base
    [ForeignKey("BaseEventId")]
    public virtual BaseEvent? BaseEvent { get; set; }
}
```

## Which Approach Is Better?

### Inheritance (Option 1) Benefits:

- More natural object-oriented design
- Polymorphism: `List<BaseEvent>` can contain any event type
- Simpler code: inherits all base properties automatically
- Works well when you want to treat all events similarly

### Foreign Key (Option 2) Benefits:

- Better performance for some query patterns
- Cleaner database structure
- More flexibility for changing individual event schemas
- More explicit control over relationships

## Recommendation

For your event system where you'll have many different event types, I recommend **using inheritance (Option 1)** because:

1. It aligns with the natural domain model (events are specialized types of a base event)
2. It enables polymorphic handling of events
3. It requires less code and is more maintainable
4. It makes querying by base properties more straightforward

Just be aware that with Table-Per-Type inheritance, queries that retrieve multiple event types may involve joins across multiple tables.

If you choose inheritance, make sure to update all event types to inherit from `BaseEvent` rather than having separate foreign key properties.

CAG-nolan: why does it say:
```
Class 'MessageEvent' is never instantiate
```

GitHub Copilot: # Resolving "Class is Never Instantiated" Warning

That warning occurs when the compiler detects a class in your codebase that's never actually created with the `new` keyword. Here are some ways to address it:

## Option 1: Create Event Factory Class

The best approach is to implement your `EventFactory` class that will create these event instances based on the incoming event type:

```csharp
// EventFactory.cs
public class EventFactory : IEventFactory
{
    public BaseEvent CreateEvent(string eventType, JsonElement data)
    {
        return eventType switch
        {
            "MessageCreate" => JsonSerializer.Deserialize<MessageEvent>(data.GetRawText()) ?? new MessageEvent(),
            "MessageUpdate" => JsonSerializer.Deserialize<MessageEvent>(data.GetRawText()) ?? new MessageEvent(),
            "CommandExecute" => JsonSerializer.Deserialize<CommandEvent>(data.GetRawText()) ?? new CommandEvent(),
            // Other event types
            _ => throw new ArgumentException($"Unknown event type: {eventType}")
        };
    }
}
```

## Option 2: Create Test Data

Add a class that creates test data in your unit tests:

```csharp
// EventTestData.cs in test project
public static class EventTestData
{
    public static MessageEvent CreateSampleMessageEvent()
    {
        return new MessageEvent
        {
            EventType = "MessageCreate",
            Timestamp = DateTime.UtcNow,
            ServerId = "123456",
            ChannelId = "789012",
            UserId = "345678",
            MessageId = "901234",
            Content = "Hello world",
            // Other properties
        };
    }
    
    // Other test data methods
}
```

## Option 3: Implement Repository Methods

Include code in your repositories that creates these events:

```csharp
// MessageRepository.cs
public class MessageRepository : Repository<MessageEvent>, IMessageRepository
{
    // Other repository methods...
    
    public async Task<MessageEvent> CreateNewMessage(string content, string userId, string channelId)
    {
        var messageEvent = new MessageEvent
        {
            EventType = "MessageCreate",
            Timestamp = DateTime.UtcNow,
            ServerId = GetServerIdFromChannel(channelId),
            ChannelId = channelId,
            UserId = userId,
            MessageId = Guid.NewGuid().ToString(),
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await Create(messageEvent);
        return messageEvent;
    }
}
```

## Option 4: Suppress the Warning

If you're sure the class will be used (through reflection, dependency injection, or future code):

```csharp
#pragma warning disable CS0649 // Type is never instantiated
public class MessageEvent : BaseEvent
{
    // Class implementation
}
#pragma warning restore CS0649
```

## Best Practice

The warning indicates you haven't fully implemented the event processing system yet. As you flesh out your:

1. Event factories
2. WebSocket message handlers
3. Event processors
4. Unit tests

...you'll naturally create instances of these classes and the warnings will disappear. It's best to address them by actually implementing the code that uses these classes rather than suppressing the warnings.

CAG-nolan: Can you update these to then use the inheretence method you describe above?:
```
# Event Model Hierarchy

Here's a comprehensive model structure for the event system, starting with the `BaseEvent` and specialized event types.

## Base Event Model

```csharp
// BaseEvent.cs
public class BaseEvent
{
    public int Id { get; set; }
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string ServerId { get; set; }
    public string ChannelId { get; set; }
    public string UserId { get; set; }
    public string RawData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Additional metadata for system use
    [NotMapped]
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
```

## Message Events

```csharp
// MessageEvent.cs
public class MessageEvent : BaseEvent
{
    public int BaseEventId { get; set; }  // Foreign key to BaseEvent
    public string MessageId { get; set; }
    public string Content { get; set; }
    public string Mentions { get; set; }  // Serialized list of user IDs
    public string Attachments { get; set; }  // Serialized attachment data
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    
    // Helper methods for serialized collections
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(Mentions);
        
    public void SetMentionsList(List<string> mentions) => 
        Mentions = JsonSerializer.Serialize(mentions);
        
    public List<AttachmentInfo> GetAttachmentsList() =>
        string.IsNullOrEmpty(Attachments) ? new List<AttachmentInfo>() : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments);
        
    public void SetAttachmentsList(List<AttachmentInfo> attachments) =>
        Attachments = JsonSerializer.Serialize(attachments);
}

// Supporting model for attachments
public class AttachmentInfo
{
    public string Id { get; set; }
    public string Url { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; }
}
```

## Command Events

```csharp
// CommandEvent.cs
public class CommandEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string CommandName { get; set; }
    public string Arguments { get; set; }  // Serialized command arguments
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; }
    
    // Helper methods for arguments
    public Dictionary<string, object> GetArgumentsDict() =>
        string.IsNullOrEmpty(Arguments) ? new Dictionary<string, object>() : 
            JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments);
            
    public void SetArgumentsDict(Dictionary<string, object> args) =>
        Arguments = JsonSerializer.Serialize(args);
}
```

## Reaction Events

```csharp
// ReactionEvent.cs
public class ReactionEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string MessageId { get; set; }
    public string EmojiName { get; set; }
    public string EmojiId { get; set; }
    public bool IsAdded { get; set; }  // True if added, false if removed
}
```

## User Events

```csharp
// UserEvent.cs
public class UserEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Join, Leave, Ban, Kick, etc.
    public string AdditionalInfo { get; set; }  // Serialized additional info
    
    // Helper for additional info
    public Dictionary<string, object> GetAdditionalInfoDict() =>
        string.IsNullOrEmpty(AdditionalInfo) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(AdditionalInfo);
            
    public void SetAdditionalInfoDict(Dictionary<string, object> info) =>
        AdditionalInfo = JsonSerializer.Serialize(info);
}
```

## Voice Events

```csharp
// VoiceEvent.cs
public class VoiceEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Join, Leave, Mute, Deafen, etc.
    public string VoiceChannelId { get; set; }
    public int DurationSeconds { get; set; }  // Only applicable for certain events
}
```

## Metric Events

```csharp
// MetricEvent.cs
public class MetricEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string MetricName { get; set; }
    public float MetricValue { get; set; }
    public string Tags { get; set; }  // Serialized tags
    
    // Helper for tags
    public Dictionary<string, string> GetTagsDict() =>
        string.IsNullOrEmpty(Tags) ? new Dictionary<string, string>() :
            JsonSerializer.Deserialize<Dictionary<string, string>>(Tags);
            
    public void SetTagsDict(Dictionary<string, string> tags) =>
        Tags = JsonSerializer.Serialize(tags);
}
```

## Guild Events

```csharp
// GuildEvent.cs - For server/guild level events
public class GuildEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Create, Update, Delete, etc.
    public string GuildId { get; set; }
    public string Changes { get; set; }  // Serialized changes to guild properties
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(Changes);
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
}
```

## Channel Events

```csharp
// ChannelEvent.cs - For channel level events
public class ChannelEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Create, Update, Delete, etc.
    public string ChannelType { get; set; }  // Text, Voice, Category, etc.
    public string ParentId { get; set; }  // Parent category ID if applicable
    public string Changes { get; set; }  // Serialized changes
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(Changes);
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
}
```

## Role Events

```csharp
// RoleEvent.cs
public class RoleEvent : BaseEvent
{
    public int BaseEventId { get; set; }
    public string EventName { get; set; }  // Create, Update, Delete, Assign, Remove
    public string RoleId { get; set; }
    public string TargetUserId { get; set; }  // For assign/remove events
    public string Changes { get; set; }  // For update events
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) ? new Dictionary<string, object>() :
            JsonSerializer.Deserialize<Dictionary<string, object>>(Changes);
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
}
```

## DbContext Configuration

```csharp
// ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<BaseEvent> BaseEvents { get; set; }
    public DbSet<MessageEvent> MessageEvents { get; set; }
    public DbSet<CommandEvent> CommandEvents { get; set; }
    public DbSet<ReactionEvent> ReactionEvents { get; set; }
    public DbSet<UserEvent> UserEvents { get; set; }
    public DbSet<VoiceEvent> VoiceEvents { get; set; }
    public DbSet<MetricEvent> MetricEvents { get; set; }
    public DbSet<GuildEvent> GuildEvents { get; set; }
    public DbSet<ChannelEvent> ChannelEvents { get; set; }
    public DbSet<RoleEvent> RoleEvents { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships
        modelBuilder.Entity<MessageEvent>()
            .HasOne<BaseEvent>()
            .WithMany()
            .HasForeignKey(e => e.BaseEventId);
            
        modelBuilder.Entity<CommandEvent>()
            .HasOne<BaseEvent>()
            .WithMany()
            .HasForeignKey(e => e.BaseEventId);
            
        // Configure remaining relationships similarly
        
        // Configure serialized JSON properties
        modelBuilder.Entity<BaseEvent>()
            .Property(e => e.RawData)
            .HasColumnType("nvarchar(max)");
            
        modelBuilder.Entity<MessageEvent>()
            .Property(e => e.Mentions)
            .HasColumnType("nvarchar(max)");
            
        modelBuilder.Entity<MessageEvent>()
            .Property(e => e.Attachments)
            .HasColumnType("nvarchar(max)");
            
        // Configure remaining serialized columns similarly
    }
}
```

## Key Design Features

1. **Inheritance**: All event types extend from BaseEvent
2. **Foreign Key Relationships**: Each specialized event links to its base event
3. **JSON Serialization Helpers**: Convenient methods to work with serialized collections
4. **Clean Type Separation**: Each event type has its own class with relevant properties
5. **Expandability**: You can add new event types by creating new derived classes

This model structure provides a clean way to represent all your event types while maintaining the relational database schema.

When extending with new event subtypes (like MessageCreate vs MessageUpdate), you can:
1. Use the same event class with a different EventType string
2. Or subclass further if the structure differs significantly

This approach gives you both type safety and database efficiency while allowing for easy expansion of your event system.
```

GitHub Copilot: # Entity Framework Models Using Inheritance (TPT)

Here's the updated model structure using proper inheritance (Table-Per-Type) for Entity Framework:

## Base Event Model

```csharp
// BaseEvent.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("BaseEvents")]
public class BaseEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    [MaxLength(50)]
    public string? ServerId { get; set; }
    
    [MaxLength(50)]
    public string? ChannelId { get; set; }
    
    [MaxLength(50)]
    public string? UserId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? RawData { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; }
    
    [Required]
    public DateTime UpdatedAt { get; set; }
    
    // Additional metadata for system use
    [NotMapped]
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
```

## Message Events

```csharp
// MessageEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("MessageEvents")]
public class MessageEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Content { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Mentions { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Attachments { get; set; }
    
    public bool IsEdited { get; set; }
    
    public bool IsDeleted { get; set; }
    
    // Helper methods for serialized collections
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(Mentions) ?? new List<string>();
        
    public void SetMentionsList(List<string> mentions) => 
        Mentions = JsonSerializer.Serialize(mentions);
        
    public List<AttachmentInfo> GetAttachmentsList() =>
        string.IsNullOrEmpty(Attachments) 
            ? new List<AttachmentInfo>() 
            : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments) ?? new List<AttachmentInfo>();
        
    public void SetAttachmentsList(List<AttachmentInfo> attachments) =>
        Attachments = JsonSerializer.Serialize(attachments);
    
    // NotMapped properties
    [NotMapped]
    public List<string> MentionsList
    {
        get => GetMentionsList();
        set => SetMentionsList(value);
    }
    
    [NotMapped]
    public List<AttachmentInfo> AttachmentsList
    {
        get => GetAttachmentsList();
        set => SetAttachmentsList(value);
    }
}

// Supporting model for attachments
public class AttachmentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
}
```

## Command Events

```csharp
// CommandEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("CommandEvents")]
public class CommandEvent : BaseEvent
{
    [Required]
    [MaxLength(100)]
    public string CommandName { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Arguments { get; set; }
    
    public bool IsSuccess { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }
    
    public int ExecutionTimeMs { get; set; }
    
    // Helper methods for arguments
    public Dictionary<string, object> GetArgumentsDict() =>
        string.IsNullOrEmpty(Arguments) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments) ?? new Dictionary<string, object>();
            
    public void SetArgumentsDict(Dictionary<string, object> args) =>
        Arguments = JsonSerializer.Serialize(args);
    
    [NotMapped]
    public Dictionary<string, object> ArgumentsDict
    {
        get => GetArgumentsDict();
        set => SetArgumentsDict(value);
    }
}
```

## Reaction Events

```csharp
// ReactionEvent.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ReactionEvents")]
public class ReactionEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string EmojiName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? EmojiId { get; set; }
    
    public bool IsAdded { get; set; }  // True if added, false if removed
}
```

## User Events

```csharp
// UserEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("UserEvents")]
public class UserEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Join, Leave, Ban, etc.
    
    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalInfo { get; set; }
    
    // Helper for additional info
    public Dictionary<string, object> GetAdditionalInfoDict() =>
        string.IsNullOrEmpty(AdditionalInfo) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(AdditionalInfo) ?? new Dictionary<string, object>();
            
    public void SetAdditionalInfoDict(Dictionary<string, object> info) =>
        AdditionalInfo = JsonSerializer.Serialize(info);
    
    [NotMapped]
    public Dictionary<string, object> AdditionalInfoDict
    {
        get => GetAdditionalInfoDict();
        set => SetAdditionalInfoDict(value);
    }
}
```

## Voice Events

```csharp
// VoiceEvent.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("VoiceEvents")]
public class VoiceEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Join, Leave, Mute, etc.
    
    [Required]
    [MaxLength(50)]
    public string VoiceChannelId { get; set; } = string.Empty;
    
    public int DurationSeconds { get; set; }
}
```

## Metric Events

```csharp
// MetricEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("MetricEvents")]
public class MetricEvent : BaseEvent
{
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;
    
    public float MetricValue { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Tags { get; set; }
    
    // Helper for tags
    public Dictionary<string, string> GetTagsDict() =>
        string.IsNullOrEmpty(Tags) 
            ? new Dictionary<string, string>() 
            : JsonSerializer.Deserialize<Dictionary<string, string>>(Tags) ?? new Dictionary<string, string>();
            
    public void SetTagsDict(Dictionary<string, string> tags) =>
        Tags = JsonSerializer.Serialize(tags);
    
    [NotMapped]
    public Dictionary<string, string> TagsDict
    {
        get => GetTagsDict();
        set => SetTagsDict(value);
    }
}
```

## Guild Events

```csharp
// GuildEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("GuildEvents")]
public class GuildEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Create, Update, Delete, etc.
    
    [Required]
    [MaxLength(50)]
    public string GuildId { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Changes { get; set; }
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Changes) ?? new Dictionary<string, object>();
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
    
    [NotMapped]
    public Dictionary<string, object> ChangesDict
    {
        get => GetChangesDict();
        set => SetChangesDict(value);
    }
}
```

## Channel Events

```csharp
// ChannelEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("ChannelEvents")]
public class ChannelEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Create, Update, Delete, etc.
    
    [Required]
    [MaxLength(50)]
    public string ChannelType { get; set; } = string.Empty;  // Text, Voice, Category, etc.
    
    [MaxLength(50)]
    public string? ParentId { get; set; }  // Parent category ID if applicable
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Changes { get; set; }
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Changes) ?? new Dictionary<string, object>();
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
    
    [NotMapped]
    public Dictionary<string, object> ChangesDict
    {
        get => GetChangesDict();
        set => SetChangesDict(value);
    }
}
```

## Role Events

```csharp
// RoleEvent.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("RoleEvents")]
public class RoleEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Create, Update, Delete, etc.
    
    [Required]
    [MaxLength(50)]
    public string RoleId { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? TargetUserId { get; set; }  // For assign/remove events
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Changes { get; set; }
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Changes) ?? new Dictionary<string, object>();
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
    
    [NotMapped]
    public Dictionary<string, object> ChangesDict
    {
        get => GetChangesDict();
        set => SetChangesDict(value);
    }
}
```

## DbContext Configuration

```csharp
// ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using System;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<BaseEvent> BaseEvents { get; set; } = null!;
    public DbSet<MessageEvent> MessageEvents { get; set; } = null!;
    public DbSet<CommandEvent> CommandEvents { get; set; } = null!;
    public DbSet<ReactionEvent> ReactionEvents { get; set; } = null!;
    public DbSet<UserEvent> UserEvents { get; set; } = null!;
    public DbSet<VoiceEvent> VoiceEvents { get; set; } = null!;
    public DbSet<MetricEvent> MetricEvents { get; set; } = null!;
    public DbSet<GuildEvent> GuildEvents { get; set; } = null!;
    public DbSet<ChannelEvent> ChannelEvents { get; set; } = null!;
    public DbSet<RoleEvent> RoleEvents { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Table-Per-Type inheritance
        modelBuilder.Entity<BaseEvent>().ToTable("BaseEvents");
        modelBuilder.Entity<MessageEvent>().ToTable("MessageEvents");
        modelBuilder.Entity<CommandEvent>().ToTable("CommandEvents");
        modelBuilder.Entity<ReactionEvent>().ToTable("ReactionEvents");
        modelBuilder.Entity<UserEvent>().ToTable("UserEvents");
        modelBuilder.Entity<VoiceEvent>().ToTable("VoiceEvents");
        modelBuilder.Entity<MetricEvent>().ToTable("MetricEvents");
        modelBuilder.Entity<GuildEvent>().ToTable("GuildEvents");
        modelBuilder.Entity<ChannelEvent>().ToTable("ChannelEvents");
        modelBuilder.Entity<RoleEvent>().ToTable("RoleEvents");
        
        // Configure indexes for common query patterns
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.EventType);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.Timestamp);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.ServerId);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.ChannelId);
            
        modelBuilder.Entity<MessageEvent>()
            .HasIndex(e => e.MessageId);
            
        modelBuilder.Entity<CommandEvent>()
            .HasIndex(e => e.CommandName);
    }
}
```

## Key Changes Made:

1. **Removed `BaseEventId` properties** - All event types now inherit Id from BaseEvent
2. **Added proper nullability**:
   - Non-nullable required fields have default values
   - Nullable fields are marked with `?`

3. **Added data annotations**:
   - `[Table]` to specify table names
   - `[Required]` and `[MaxLength]` for constraints
   - `[Column]` for column types

4. **Added null-handling in serialization methods**:
   - Added `?? new List<string>()` to handle null results from deserialization

5. **Added NotMapped properties**:
   - Added properties for serialized collections with getters/setters

6. **DbContext configuration**:
   - Explicitly configured Table-Per-Type inheritance
   - Added indexes for common query patterns
   - Used `null!` annotation for DbSet properties to suppress nullable warnings

7. **Added using directives**:
   - Added required namespace imports to each file

This updated model structure uses proper inheritance according to Entity Framework conventions, avoids nullable reference warnings, and maintains all the functionality of the original design.

CAG-nolan: Lets say i want to track if a message is updated/deleted. How should i best do this? Should i make another table that tracks these events? How would i tie them back to the original message?

GitHub Copilot: # Tracking Message Updates and Deletions

For tracking message updates and deletions, I recommend a hybrid approach that combines event tracking with message relationship tracking. Here are two effective options:

## Option 1: Event Types with RelatedMessageId (Recommended)

Create specific event types while maintaining relationships between related message events:

```csharp
// MessageEvent.cs - Add fields to track relationships
[Table("MessageEvents")]
public class MessageEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Content { get; set; }
    
    // Other existing properties...
    
    // Add fields to track message history
    [MaxLength(50)]
    public string? OriginalMessageId { get; set; }
    
    [MaxLength(50)]
    public string? PreviousMessageId { get; set; }
    
    public MessageOperation Operation { get; set; } = MessageOperation.Create;
    
    // Helper methods and NotMapped properties...
}

// MessageOperation.cs
public enum MessageOperation
{
    Create,
    Update,
    Delete
}
```

In your DbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Existing configuration...
    
    // Add indexes for message relationship queries
    modelBuilder.Entity<MessageEvent>()
        .HasIndex(e => e.OriginalMessageId);
        
    modelBuilder.Entity<MessageEvent>()
        .HasIndex(e => e.PreviousMessageId);
}
```

### How to use this approach:

1. **For message creation**:
   ```csharp
   var messageEvent = new MessageEvent
   {
       EventType = "MessageCreate",
       MessageId = "msg123",
       Operation = MessageOperation.Create,
       Content = "Hello world",
       // Other properties...
   };
   ```

2. **For message updates**:
   ```csharp
   var updateEvent = new MessageEvent
   {
       EventType = "MessageUpdate",
       MessageId = "msg123-edit1",  // New unique ID for this event
       OriginalMessageId = "msg123", // Points to the original message
       PreviousMessageId = "msg123", // Points to the immediate previous version
       Operation = MessageOperation.Update,
       Content = "Hello updated world",
       // Other properties...
   };
   ```

3. **For message deletions**:
   ```csharp
   var deleteEvent = new MessageEvent
   {
       EventType = "MessageDelete",
       MessageId = "msg123-delete",  // New unique ID for this event
       OriginalMessageId = "msg123", // Points to the original message
       PreviousMessageId = "msg123-edit1", // Points to the last known version
       Operation = MessageOperation.Delete,
       Content = null,  // Content might be nulled on deletion
       IsDeleted = true,
       // Other properties...
   };
   ```

## Option 2: Separate MessageHistory Table

Another approach is to keep a separate table for message history:

```csharp
// MessageHistoryEvent.cs
[Table("MessageHistoryEvents")]
public class MessageHistoryEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string OriginalMessageId { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? PreviousContent { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? NewContent { get; set; }
    
    [Required]
    public MessageOperation Operation { get; set; }
    
    [Required]
    public DateTime OperationTimestamp { get; set; }
    
    // Navigation property to the original message
    [NotMapped]
    public MessageEvent? OriginalMessage { get; set; }
}
```

## Querying Message History

With Option 1, you can query the message history like this:

```csharp
// Get the complete history of a message
public async Task<List<MessageEvent>> GetMessageHistory(string originalMessageId)
{
    return await _dbContext.MessageEvents
        .Where(m => m.OriginalMessageId == originalMessageId || m.MessageId == originalMessageId)
        .OrderBy(m => m.Timestamp)
        .ToListAsync();
}

// Get the latest version of a message
public async Task<MessageEvent?> GetLatestMessageVersion(string originalMessageId)
{
    return await _dbContext.MessageEvents
        .Where(m => m.OriginalMessageId == originalMessageId || m.MessageId == originalMessageId)
        .OrderByDescending(m => m.Timestamp)
        .FirstOrDefaultAsync();
}
```

## Repository Implementation

```csharp
public class MessageRepository : Repository<MessageEvent>, IMessageRepository
{
    public MessageRepository(ApplicationDbContext dbContext) : base(dbContext) { }
    
    public async Task<int> CreateMessageUpdateEvent(string originalMessageId, string content, string userId)
    {
        // Find the latest version of the message
        var latestVersion = await GetLatestMessageVersion(originalMessageId);
        if (latestVersion == null)
            throw new KeyNotFoundException($"Original message {originalMessageId} not found");
            
        // Create a new message update event
        var updateEvent = new MessageEvent
        {
            EventType = "MessageUpdate",
            MessageId = $"{originalMessageId}-edit-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            OriginalMessageId = originalMessageId,
            PreviousMessageId = latestVersion.MessageId,
            Operation = MessageOperation.Update,
            Content = content,
            Timestamp = DateTime.UtcNow,
            ServerId = latestVersion.ServerId,
            ChannelId = latestVersion.ChannelId,
            UserId = userId,  // Could be different than original creator
            IsEdited = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _dbContext.MessageEvents.AddAsync(updateEvent);
        await _dbContext.SaveChangesAsync();
        
        return updateEvent.Id;
    }
    
    public async Task<int> CreateMessageDeleteEvent(string originalMessageId, string userId)
    {
        // Find the latest version of the message
        var latestVersion = await GetLatestMessageVersion(originalMessageId);
        if (latestVersion == null)
            throw new KeyNotFoundException($"Original message {originalMessageId} not found");
            
        // Create a new message delete event
        var deleteEvent = new MessageEvent
        {
            EventType = "MessageDelete",
            MessageId = $"{originalMessageId}-delete-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            OriginalMessageId = originalMessageId,
            PreviousMessageId = latestVersion.MessageId,
            Operation = MessageOperation.Delete,
            Content = latestVersion.Content,  // Optional: preserve content or set to null
            Timestamp = DateTime.UtcNow,
            ServerId = latestVersion.ServerId,
            ChannelId = latestVersion.ChannelId,
            UserId = userId,  // Could be different than original creator
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _dbContext.MessageEvents.AddAsync(deleteEvent);
        await _dbContext.SaveChangesAsync();
        
        return deleteEvent.Id;
    }
}
```

## Recommendation

I recommend Option 1 (Event Types with RelatedMessageId) because:

1. **It maintains the event-driven architecture** you've established
2. **Provides complete history** of all message changes
3. **Tracks who made each change** and when
4. **Simplifies relational queries** between message versions
5. **Doesn't require separate tables** that need to be kept in sync

This approach allows you to capture all message events while maintaining relationships between them, giving you the best of both worlds: a complete history and easy navigation between related events.

CAG-nolan: my ApplicationDbContext throws `Cannot resolve symbol 'ToTable'` for things like `modelBuilder.Entity<BaseEvent>().ToTable("BaseEvents");`. Can you please recommend how to fix this?

GitHub Copilot: # Fixing "Cannot resolve symbol 'ToTable'" Error

This error occurs because you're missing the Entity Framework Core Relational extension package. The `ToTable()` method is an extension method provided by the EF Core Relational extensions.

## Solution:

### 1. Add the Required NuGet Package

```bash
dotnet add package Microsoft.EntityFrameworkCore.Relational
```

### 2. Make sure your ApplicationDbContext.cs has the correct imports:

```csharp
// ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
```

### 3. Alternative Approach (if you can't add the package)

If for some reason you can't add the Relational package, you can modify your OnModelCreating method to avoid using ToTable:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Instead of ToTable, use the discriminator approach
    modelBuilder.Entity<BaseEvent>()
        .HasDiscriminator<string>("EventType")
        .HasValue<MessageEvent>("Message")
        .HasValue<CommandEvent>("Command")
        .HasValue<ReactionEvent>("Reaction")
        .HasValue<UserEvent>("User")
        .HasValue<VoiceEvent>("Voice")
        .HasValue<MetricEvent>("Metric")
        .HasValue<GuildEvent>("Guild")
        .HasValue<ChannelEvent>("Channel")
        .HasValue<RoleEvent>("Role");
    
    // Configure indexes for common query patterns
    modelBuilder.Entity<BaseEvent>()
        .HasIndex(e => e.EventType);
        
    modelBuilder.Entity<BaseEvent>()
        .HasIndex(e => e.Timestamp);
    
    // Other index configurations...
}
```

### 4. Check Your Project References

Make sure your project file (.csproj) includes the following references:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="6.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="6.0.0" />
  <!-- If using SQL Server -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="6.0.0" />
</ItemGroup>
```

Replace the version numbers with the version you're using. Make sure all EF Core packages have matching versions.

### 5. Full ApplicationDbContext.cs Example with Imports

```csharp
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<BaseEvent> BaseEvents { get; set; } = null!;
    public DbSet<MessageEvent> MessageEvents { get; set; } = null!;
    public DbSet<CommandEvent> CommandEvents { get; set; } = null!;
    // Other DbSets...
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Table-Per-Type inheritance
        modelBuilder.Entity<BaseEvent>().ToTable("BaseEvents");
        modelBuilder.Entity<MessageEvent>().ToTable("MessageEvents");
        modelBuilder.Entity<CommandEvent>().ToTable("CommandEvents");
        // Other ToTable configurations...
        
        // Configure indexes
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.EventType);
            
        // Other index configurations...
    }
}
```

If you still encounter issues after implementing these solutions, you might need to check if your IDE's NuGet cache needs refreshing or if your project needs rebuilding.

CAG-nolan: So, lets say my server should accept and send websockets with this shape:
```
{
    issuer: "ISSUER_TYPE",
    type: "MESSAGE_TYPE",
    data: "REQUEST_DATA",
    id: "REQUEST_ID",
    timestamp: "TIMESTAMP"
}
```
where the issuer is who sent the message (BOT, SERVER, DASHBOARD), the type is the type of message (ie MESSAGE_CREATE, MESSAGE_UPDATE, VOICE_UPDATE, etc...), data is the raw data JSON for that request type, the ID is a unique ID for the request, and the timestamp is the time the request was sent. How should I structure my application to appropriate handle the requests, ensuring a SOLID design that can handle the websocket communications and resolve/deserialize the request JSON and related message type JSON data?

GitHub Copilot: # SOLID WebSocket Communication Architecture

I'll design a comprehensive architecture to handle your WebSocket messages with a clean, extensible approach.

## 1. WebSocket Message Model

First, let's define the base message structure:

```csharp
// WebSocketMessage.cs
public class WebSocketMessage
{
    public string Issuer { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

// IssuerType.cs
public static class IssuerType
{
    public const string Bot = "BOT";
    public const string Server = "SERVER";
    public const string Dashboard = "DASHBOARD";
}

// MessageType.cs
public static class MessageType
{
    public const string MessageCreate = "MESSAGE_CREATE";
    public const string MessageUpdate = "MESSAGE_UPDATE";
    public const string MessageDelete = "MESSAGE_DELETE";
    public const string VoiceUpdate = "VOICE_UPDATE";
    // Add more message types as needed
}
```

## 2. Message Handler Interfaces

```csharp
// IMessageHandler.cs
public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleMessageAsync(WebSocketMessage message, WebSocket socket);
}

// IMessageHandlerRegistry.cs
public interface IMessageHandlerRegistry
{
    void RegisterHandler(IMessageHandler handler);
    IMessageHandler GetHandler(string messageType);
    bool HasHandler(string messageType);
}
```

## 3. WebSocket Manager

```csharp
// WebSocketConnection.cs
public class WebSocketConnection
{
    public string ConnectionId { get; set; }
    public WebSocket Socket { get; set; }
    public string? UserId { get; set; }
    public string? ServerId { get; set; }
    public DateTime ConnectedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// IWebSocketManager.cs
public interface IWebSocketManager
{
    Task AddSocketAsync(WebSocket socket, HttpContext context);
    Task RemoveSocketAsync(string connectionId);
    Task SendToSocketAsync(string connectionId, object data);
    Task SendToAllAsync(object data);
    Task SendToIssuerAsync(string issuer, object data);
    IEnumerable<WebSocketConnection> GetAllSockets();
}
```

## 4. Message Serialization Service

```csharp
// IMessageSerializationService.cs
public interface IMessageSerializationService
{
    string Serialize<T>(T message);
    T? Deserialize<T>(string json);
    WebSocketMessage? DeserializeMessage(string json);
    TData? DeserializeData<TData>(JsonElement dataElement);
}

// MessageSerializationService.cs
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
```

## 5. WebSocket Controller

```csharp
// WebSocketController.cs
public class WebSocketController
{
    private readonly IWebSocketManager _webSocketManager;
    private readonly IMessageHandlerRegistry _messageHandlerRegistry;
    private readonly IMessageSerializationService _serializationService;
    private readonly ILogger<WebSocketController> _logger;
    
    public WebSocketController(
        IWebSocketManager webSocketManager,
        IMessageHandlerRegistry messageHandlerRegistry,
        IMessageSerializationService serializationService,
        ILogger<WebSocketController> logger)
    {
        _webSocketManager = webSocketManager;
        _messageHandlerRegistry = messageHandlerRegistry;
        _serializationService = serializationService;
        _logger = logger;
    }
    
    public async Task HandleConnection(WebSocket socket, HttpContext context)
    {
        await _webSocketManager.AddSocketAsync(socket, context);
        
        var buffer = new byte[4096];
        WebSocketReceiveResult? result = null;
        
        try
        {
            do
            {
                using var ms = new MemoryStream();
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync();
                    await ProcessMessage(message, socket);
                }
            }
            while (socket.State == WebSocketState.Open && result?.MessageType != WebSocketMessageType.Close);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error");
        }
        finally
        {
            var connection = _webSocketManager.GetAllSockets().FirstOrDefault(c => c.Socket == socket);
            if (connection != null)
            {
                await _webSocketManager.RemoveSocketAsync(connection.ConnectionId);
            }
            
            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }
    }
    
    private async Task ProcessMessage(string message, WebSocket socket)
    {
        try
        {
            var wsMessage = _serializationService.DeserializeMessage(message);
            if (wsMessage == null)
            {
                _logger.LogWarning("Received invalid message format");
                return;
            }
            
            _logger.LogInformation($"Received message: Type={wsMessage.Type}, Id={wsMessage.Id}");
            
            if (_messageHandlerRegistry.HasHandler(wsMessage.Type))
            {
                var handler = _messageHandlerRegistry.GetHandler(wsMessage.Type);
                await handler.HandleMessageAsync(wsMessage, socket);
            }
            else
            {
                _logger.LogWarning($"No handler registered for message type: {wsMessage.Type}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket message");
        }
    }
}
```

## 6. Message Handler Registry Implementation

```csharp
// MessageHandlerRegistry.cs
public class MessageHandlerRegistry : IMessageHandlerRegistry
{
    private readonly Dictionary<string, IMessageHandler> _handlers = new();
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
```

## 7. WebSocket Manager Implementation

```csharp
// WebSocketManager.cs
public class WebSocketManager : IWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _sockets = new();
    private readonly IMessageSerializationService _serializationService;
    private readonly ILogger<WebSocketManager> _logger;
    
    public WebSocketManager(
        IMessageSerializationService serializationService,
        ILogger<WebSocketManager> logger)
    {
        _serializationService = serializationService;
        _logger = logger;
    }
    
    public async Task AddSocketAsync(WebSocket socket, HttpContext context)
    {
        var connectionId = Guid.NewGuid().ToString();
        var userId = context.User.Identity?.IsAuthenticated == true 
            ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            : null;
            
        var connection = new WebSocketConnection
        {
            ConnectionId = connectionId,
            Socket = socket,
            UserId = userId,
            ConnectedAt = DateTime.UtcNow
        };
        
        _sockets.TryAdd(connectionId, connection);
        _logger.LogInformation($"Socket added: {connectionId}, User: {userId ?? "anonymous"}");
    }
    
    public async Task RemoveSocketAsync(string connectionId)
    {
        if (_sockets.TryRemove(connectionId, out var connection))
        {
            _logger.LogInformation($"Socket removed: {connectionId}");
        }
    }
    
    public async Task SendToSocketAsync(string connectionId, object data)
    {
        if (_sockets.TryGetValue(connectionId, out var connection))
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                var serialized = _serializationService.Serialize(data);
                var buffer = Encoding.UTF8.GetBytes(serialized);
                await connection.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }
    
    public async Task SendToAllAsync(object data)
    {
        var serialized = _serializationService.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var tasks = _sockets.Values
            .Where(s => s.Socket.State == WebSocketState.Open)
            .Select(s => s.Socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None));
                
        await Task.WhenAll(tasks);
    }
    
    public async Task SendToIssuerAsync(string issuer, object data)
    {
        var serialized = _serializationService.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var tasks = _sockets.Values
            .Where(s => s.Socket.State == WebSocketState.Open && 
                   s.Metadata.TryGetValue("Issuer", out var socketIssuer) && 
                   socketIssuer?.ToString() == issuer)
            .Select(s => s.Socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None));
                
        await Task.WhenAll(tasks);
    }
    
    public IEnumerable<WebSocketConnection> GetAllSockets()
    {
        return _sockets.Values;
    }
}
```

## 8. Message Handlers for Different Message Types

```csharp
// MessageCreateHandler.cs
public class MessageCreateHandler : IMessageHandler
{
    private readonly IMessageSerializationService _serializationService;
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    
    public string MessageType => MessageType.MessageCreate;
    
    public MessageCreateHandler(
        IMessageSerializationService serializationService,
        IEventService eventService,
        IRelayService relayService)
    {
        _serializationService = serializationService;
        _eventService = eventService;
        _relayService = relayService;
    }
    
    public async Task HandleMessageAsync(WebSocketMessage message, WebSocket socket)
    {
        // Deserialize the specific data for this message type
        var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
        if (messageData == null) return;
        
        // Set event base properties from the WebSocket message
        messageData.EventType = MessageType;
        messageData.Timestamp = message.Timestamp;
        messageData.CreatedAt = DateTime.UtcNow;
        messageData.UpdatedAt = DateTime.UtcNow;
        
        // Process and store the event
        await _eventService.ProcessEvent(messageData);
        
        // Relay to other services
        await _relayService.RelayEvent(messageData);
    }
}

// MessageUpdateHandler.cs
public class MessageUpdateHandler : IMessageHandler
{
    private readonly IMessageSerializationService _serializationService;
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    private readonly IMessageRepository _messageRepository;
    
    public string MessageType => MessageType.MessageUpdate;
    
    public MessageUpdateHandler(
        IMessageSerializationService serializationService,
        IEventService eventService,
        IRelayService relayService,
        IMessageRepository messageRepository)
    {
        _serializationService = serializationService;
        _eventService = eventService;
        _relayService = relayService;
        _messageRepository = messageRepository;
    }
    
    public async Task HandleMessageAsync(WebSocketMessage message, WebSocket socket)
    {
        // Deserialize the specific data for this message type
        var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
        if (messageData == null) return;
        
        // Mark as an edit
        messageData.IsEdited = true;
        
        // Set event properties
        messageData.EventType = MessageType;
        messageData.Timestamp = message.Timestamp;
        messageData.CreatedAt = DateTime.UtcNow;
        messageData.UpdatedAt = DateTime.UtcNow;
        
        // Find original message and establish relationship
        var originalMessage = await _messageRepository.GetByMessageId(messageData.MessageId);
        if (originalMessage != null)
        {
            messageData.OriginalMessageId = originalMessage.MessageId;
            messageData.MessageId = $"{originalMessage.MessageId}-edit-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }
        
        // Process and store the event
        await _eventService.ProcessEvent(messageData);
        
        // Relay to other services
        await _relayService.RelayEvent(messageData);
    }
}

// Add more handlers for each message type
```

## 9. Dependency Injection Setup

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add core services
    services.AddSingleton<IMessageSerializationService, MessageSerializationService>();
    services.AddSingleton<IWebSocketManager, WebSocketManager>();
    services.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
    services.AddScoped<WebSocketController>();
    
    // Add event services
    services.AddScoped<IEventService, EventService>();
    services.AddScoped<IRelayService, RelayService>();
    
    // Add repositories
    services.AddScoped<IMessageRepository, MessageRepository>();
    // Add other repositories
    
    // Add message handlers
    services.AddScoped<IMessageHandler, MessageCreateHandler>();
    services.AddScoped<IMessageHandler, MessageUpdateHandler>();
    services.AddScoped<IMessageHandler, MessageDeleteHandler>();
    services.AddScoped<IMessageHandler, VoiceUpdateHandler>();
    // Add more handlers as needed
}

// Register handlers in Configure method
public void Configure(IApplicationBuilder app, IMessageHandlerRegistry registry, IServiceProvider serviceProvider)
{
    // Collect and register all handlers
    var handlers = serviceProvider.GetServices<IMessageHandler>();
    foreach (var handler in handlers)
    {
        registry.RegisterHandler(handler);
    }
    
    // WebSocket middleware
    app.UseWebSockets(new WebSocketOptions
    {
        KeepAliveInterval = TimeSpan.FromMinutes(2)
    });
    
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/ws")
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var websocket = await context.WebSockets.AcceptWebSocketAsync();
                var controller = context.RequestServices.GetRequiredService<WebSocketController>();
                await controller.HandleConnection(websocket, context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
        else
        {
            await next();
        }
    });
}
```

## 10. Response Message Generator

```csharp
// WebSocketResponseBuilder.cs
public class WebSocketResponseBuilder
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
            Type = "ERROR",
            Data = JsonSerializer.SerializeToElement(error, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Timestamp = DateTime.UtcNow
        };
    }
}
```

## Key SOLID Principles Applied

1. **Single Responsibility Principle**:
   - Each handler handles only one message type
   - WebSocketController focuses only on connection management
   - Serialization is handled by a dedicated service

2. **Open/Closed Principle**:
   - New message types can be added without modifying existing code
   - Just create a new handler and register it

3. **Liskov Substitution Principle**:
   - All message handlers implement the same interface
   - They can be used interchangeably where IMessageHandler is expected

4. **Interface Segregation Principle**:
   - Interfaces are focused and minimal
   - Clients only need to implement methods they actually use

5. **Dependency Inversion Principle**:
   - All components depend on abstractions (interfaces)
   - Implementation details can be swapped without affecting other parts

This architecture provides a clean, maintainable system for handling WebSocket messages that can easily be extended to support new message types without modifying existing code.

Similar code found with 1 license type

CAG-nolan: FOR REFERENCE, THIS WAS YOUR SOLUTION EARLIER:
---
# WebSocket Event Relay Server Design

I'll design a WebSocket server that acts as a relay and persistent storage mechanism between two services, following SOLID principles and using the controller-service-repository pattern.

## Architecture Overview

```
┌───────────┐     ┌─────────────────────┐     ┌───────────┐
│ Client    │◄────┤ WebSocket Server    │────►│ Target    │
│ Service   │     │ (Relay + Storage)   │     │ Service   │
└───────────┘     └─────────────────────┘     └───────────┘
                           │
                           ▼
                  ┌─────────────────┐
                  │ Database        │
                  └─────────────────┘
```

## Components

### Controllers

1. **WebSocketController**: Handles real-time event streaming
2. **EventApiController**: Provides HTTP endpoints for historical data

```csharp
// WebSocketController.cs
public class WebSocketController
{
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    
    public WebSocketController(IEventService eventService, IRelayService relayService)
    {
        _eventService = eventService;
        _relayService = relayService;
    }
    
    public async Task HandleConnection(WebSocket socket)
    {
        // Authentication logic here
        
        await ProcessMessages(socket);
    }
    
    private async Task ProcessMessages(WebSocket socket)
    {
        // Process incoming WebSocket messages
        // Save to storage via _eventService
        // Relay to destination via _relayService
    }
}
```

```csharp
// EventApiController.cs
[ApiController]
[Route("api/events")]
public class EventApiController : ControllerBase
{
    private readonly IQueryService _queryService;
    
    public EventApiController(IQueryService queryService)
    {
        _queryService = queryService;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] EventQueryParameters parameters)
    {
        var events = await _queryService.GetEvents(parameters);
        return Ok(events);
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetEventById(int id)
    {
        var eventData = await _queryService.GetEventById(id);
        return eventData != null ? Ok(eventData) : NotFound();
    }
    
    // Additional endpoints for specific event types
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessageEvents([FromQuery] MessageQueryParameters parameters)
    {
        var events = await _queryService.GetMessageEvents(parameters);
        return Ok(events);
    }
}
```

### Services

```csharp
// IEventService.cs - Process and validate incoming events
public interface IEventService
{
    Task<int> ProcessEvent(BaseEvent eventData);
}

// IRelayService.cs - Forward events to destination
public interface IRelayService
{
    Task RelayEvent(BaseEvent eventData);
}

// IQueryService.cs - Retrieve historical events
public interface IQueryService
{
    Task<BaseEvent> GetEventById(int id);
    Task<IEnumerable<BaseEvent>> GetEvents(EventQueryParameters parameters);
    Task<IEnumerable<MessageEvent>> GetMessageEvents(MessageQueryParameters parameters);
    // Other specialized query methods
}
```

### Event Service Implementation

```csharp
public class EventService : IEventService
{
    private readonly IEventRepository _repository;
    
    public EventService(IEventRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<int> ProcessEvent(BaseEvent eventData)
    {
        // Validate event structure
        ValidateEvent(eventData);
        
        // Set timestamps
        eventData.Timestamp = eventData.Timestamp == default ? DateTime.UtcNow : eventData.Timestamp;
        eventData.CreatedAt = DateTime.UtcNow;
        eventData.UpdatedAt = DateTime.UtcNow;
        
        // Save event
        return await _repository.SaveEvent(eventData);
    }
    
    private void ValidateEvent(BaseEvent eventData)
    {
        // Validation logic based on event type
    }
}
```

### Relay Service Implementation

```csharp
public class RelayService : IRelayService
{
    private readonly WebSocketManager _webSocketManager;
    private readonly HttpClient _httpClient;
    private readonly RelayConfig _config;
    
    public RelayService(
        WebSocketManager webSocketManager, 
        HttpClient httpClient,
        IOptions<RelayConfig> config)
    {
        _webSocketManager = webSocketManager;
        _httpClient = httpClient;
        _config = config.Value;
    }
    
    public async Task RelayEvent(BaseEvent eventData)
    {
        var json = JsonSerializer.Serialize(eventData);
        
        // Relay via WebSockets for real-time updates
        if (_config.UseWebSockets)
        {
            await _webSocketManager.SendToAllAsync(json);
        }
        
        // Also relay via HTTP if configured
        if (_config.UseHttpRelay)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(_config.RelayEndpoint, content);
        }
    }
}
```

### Repositories

```csharp
// IEventRepository.cs - Data access interface
public interface IEventRepository
{
    Task<int> SaveEvent(BaseEvent eventData);
    Task<BaseEvent> GetEventById(int id);
    Task<IEnumerable<BaseEvent>> GetEvents(EventQueryParameters parameters);
    Task<IEnumerable<MessageEvent>> GetMessageEvents(MessageQueryParameters parameters);
    // Other specialized methods for different event types
}
```

### Repository Implementation

```csharp
public class EventRepository : IEventRepository
{
    private readonly ApplicationDbContext _dbContext;
    
    public EventRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<int> SaveEvent(BaseEvent eventData)
    {
        // Save base event
        _dbContext.BaseEvents.Add(eventData);
        await _dbContext.SaveChangesAsync();
        
        // Handle specific event type
        switch (eventData.EventType)
        {
            case "Message":
                await SaveMessageEvent(eventData as MessageEvent);
                break;
            case "Command":
                await SaveCommandEvent(eventData as CommandEvent);
                break;
            // Additional event types...
        }
        
        return eventData.Id;
    }
    
    private async Task SaveMessageEvent(MessageEvent messageEvent)
    {
        if (messageEvent == null) return;
        
        messageEvent.BaseEventId = messageEvent.Id;
        _dbContext.MessageEvents.Add(messageEvent);
        await _dbContext.SaveChangesAsync();
    }
    
    // Implementation of other repository methods...
}
```

## Dependency Injection Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Database
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
    
    // Repositories
    services.AddScoped<IEventRepository, EventRepository>();
    
    // Services
    services.AddScoped<IEventService, EventService>();
    services.AddScoped<IRelayService, RelayService>();
    services.AddScoped<IQueryService, QueryService>();
    
    // Web infrastructure
    services.AddSingleton<WebSocketManager>();
    services.AddHttpClient();
    
    // Configuration
    services.Configure<RelayConfig>(Configuration.GetSection("RelayConfig"));
    
    services.AddControllers();
}
```

## WebSocket Middleware

```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var controller = context.RequestServices.GetRequiredService<WebSocketController>();
            await controller.HandleConnection(socket);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});
```

## Key Features

- **Separation of Concerns**: Controllers, services, and repositories each have distinct responsibilities
- **Real-time + Historical**: WebSockets for real-time relay, HTTP API for historical data
- **SOLID Principles**:
  - Single Responsibility: Each class has one job
  - Open/Closed: Extensible for new event types without modifying existing code
  - Liskov Substitution: Event subtypes can be used where base types are expected
  - Interface Segregation: Clean, purpose-specific interfaces
  - Dependency Inversion: High-level modules depend on abstractions

This design provides a scalable, maintainable system that fulfills all requirements while ensuring clean separation between components.
---
and
---
# Expandable Event Architecture Design

To support a growing number of event types (messages, users, guilds, etc.), I'll design a more scalable architecture that allows adding new event types without modifying existing code.

## Event Type System

### Event Registration & Factory System

```csharp
// Core event type registry
public interface IEventTypeRegistry
{
    void RegisterEventType<TEvent>(string eventType) where TEvent : BaseEvent;
    Type GetEventType(string eventType);
    bool IsRegistered(string eventType);
}

public class EventTypeRegistry : IEventTypeRegistry
{
    private readonly Dictionary<string, Type> _eventTypes = new();

    public void RegisterEventType<TEvent>(string eventType) where TEvent : BaseEvent
    {
        _eventTypes[eventType] = typeof(TEvent);
    }

    public Type GetEventType(string eventType)
    {
        return _eventTypes.TryGetValue(eventType, out var type) ? type : typeof(BaseEvent);
    }

    public bool IsRegistered(string eventType) => _eventTypes.ContainsKey(eventType);
}
```

### Event Factory

```csharp
public interface IEventFactory
{
    BaseEvent CreateEvent(string eventType, JsonElement data);
}

public class EventFactory : IEventFactory
{
    private readonly IEventTypeRegistry _eventTypeRegistry;

    public EventFactory(IEventTypeRegistry eventTypeRegistry)
    {
        _eventTypeRegistry = eventTypeRegistry;
    }

    public BaseEvent CreateEvent(string eventType, JsonElement data)
    {
        var type = _eventTypeRegistry.GetEventType(eventType);
        return (BaseEvent)JsonSerializer.Deserialize(data.GetRawText(), type);
    }
}
```

## Event Processors (Strategy Pattern)

```csharp
// Event processor interface
public interface IEventProcessor
{
    string EventType { get; }
    Task ProcessEvent(BaseEvent baseEvent);
}

// Specific event processors
public class MessageCreateProcessor : IEventProcessor
{
    private readonly IMessageRepository _repository;
    
    public string EventType => "MessageCreate";
    
    public MessageCreateProcessor(IMessageRepository repository)
    {
        _repository = repository;
    }
    
    public async Task ProcessEvent(BaseEvent baseEvent)
    {
        var messageEvent = (MessageEvent)baseEvent;
        // Process message creation specifics
        await _repository.Create(messageEvent);
    }
}

public class MessageUpdateProcessor : IEventProcessor
{
    private readonly IMessageRepository _repository;
    
    public string EventType => "MessageUpdate";
    
    public MessageUpdateProcessor(IMessageRepository repository)
    {
        _repository = repository;
    }
    
    public async Task ProcessEvent(BaseEvent baseEvent)
    {
        var messageEvent = (MessageEvent)baseEvent;
        messageEvent.IsEdited = true;
        // Process message update specifics
        await _repository.Update(messageEvent);
    }
}

// Similar processors for other event types
```

## Event Processor Registry

```csharp
public interface IEventProcessorRegistry
{
    void RegisterProcessor(IEventProcessor processor);
    IEventProcessor GetProcessor(string eventType);
}

public class EventProcessorRegistry : IEventProcessorRegistry
{
    private readonly Dictionary<string, IEventProcessor> _processors = new();
    
    public void RegisterProcessor(IEventProcessor processor)
    {
        _processors[processor.EventType] = processor;
    }
    
    public IEventProcessor GetProcessor(string eventType)
    {
        return _processors.TryGetValue(eventType, out var processor) 
            ? processor 
            : throw new KeyNotFoundException($"No processor registered for event type: {eventType}");
    }
}
```

## Generic Repository Pattern

```csharp
// Generic repository interface
public interface IRepository<TEntity> where TEntity : class
{
    Task<int> Create(TEntity entity);
    Task<TEntity> GetById(int id);
    Task<IEnumerable<TEntity>> GetAll(Func<IQueryable<TEntity>, IQueryable<TEntity>> query = null);
    Task Update(TEntity entity);
    Task Delete(int id);
}

// Generic repository implementation
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly ApplicationDbContext _dbContext;
    protected readonly DbSet<TEntity> _dbSet;
    
    public Repository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
    }
    
    public async Task<int> Create(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
        
        // Assuming entity has an Id property
        return (int)entity.GetType().GetProperty("Id").GetValue(entity);
    }
    
    public async Task<TEntity> GetById(int id)
    {
        return await _dbSet.FindAsync(id);
    }
    
    public async Task<IEnumerable<TEntity>> GetAll(Func<IQueryable<TEntity>, IQueryable<TEntity>> query = null)
    {
        IQueryable<TEntity> queryable = _dbSet;
        
        if (query != null)
            queryable = query(queryable);
            
        return await queryable.ToListAsync();
    }
    
    public async Task Update(TEntity entity)
    {
        _dbContext.Entry(entity).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task Delete(int id)
    {
        var entity = await GetById(id);
        _dbSet.Remove(entity);
        await _dbContext.SaveChangesAsync();
    }
}
```

## Specialized Repositories

```csharp
// For Message events
public interface IMessageRepository : IRepository<MessageEvent>
{
    Task<IEnumerable<MessageEvent>> GetMessagesByChannel(string channelId, DateTime? since = null);
}

public class MessageRepository : Repository<MessageEvent>, IMessageRepository
{
    public MessageRepository(ApplicationDbContext dbContext) : base(dbContext) { }
    
    public async Task<IEnumerable<MessageEvent>> GetMessagesByChannel(string channelId, DateTime? since = null)
    {
        IQueryable<MessageEvent> query = _dbSet
            .Join(_dbContext.BaseEvents,
                msg => msg.BaseEventId,
                baseEvent => baseEvent.Id,
                (msg, baseEvent) => new { Message = msg, Base = baseEvent })
            .Where(joined => joined.Base.ChannelId == channelId)
            .Select(joined => joined.Message);
            
        if (since.HasValue)
        {
            query = query.Join(_dbContext.BaseEvents,
                msg => msg.BaseEventId,
                baseEvent => baseEvent.Id,
                (msg, baseEvent) => new { Message = msg, Base = baseEvent })
                .Where(joined => joined.Base.Timestamp >= since.Value)
                .Select(joined => joined.Message);
        }
        
        return await query.ToListAsync();
    }
}

// Similar repositories for other event types
```

## Enhanced Event Service

```csharp
public class EventService : IEventService
{
    private readonly IEventTypeRegistry _eventTypeRegistry;
    private readonly IEventFactory _eventFactory;
    private readonly IEventProcessorRegistry _processorRegistry;
    private readonly IRepository<BaseEvent> _baseRepository;
    
    public EventService(
        IEventTypeRegistry eventTypeRegistry,
        IEventFactory eventFactory,
        IEventProcessorRegistry processorRegistry,
        IRepository<BaseEvent> baseRepository)
    {
        _eventTypeRegistry = eventTypeRegistry;
        _eventFactory = eventFactory;
        _processorRegistry = processorRegistry;
        _baseRepository = baseRepository;
    }
    
    public async Task<int> ProcessEvent(string eventType, JsonElement data)
    {
        // Create appropriate event instance
        var eventData = _eventFactory.CreateEvent(eventType, data);
        
        // Set common fields
        eventData.EventType = eventType;
        eventData.Timestamp = eventData.Timestamp == default ? DateTime.UtcNow : eventData.Timestamp;
        eventData.CreatedAt = DateTime.UtcNow;
        eventData.UpdatedAt = DateTime.UtcNow;
        
        // Save base event first
        var baseEventId = await _baseRepository.Create(eventData);
        eventData.Id = baseEventId;
        
        // Process with the appropriate handler
        var processor = _processorRegistry.GetProcessor(eventType);
        await processor.ProcessEvent(eventData);
        
        return baseEventId;
    }
}
```

## WebSocket Controller Improvement

```csharp
public class WebSocketController
{
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    private readonly ILogger<WebSocketController> _logger;
    
    public WebSocketController(
        IEventService eventService, 
        IRelayService relayService,
        ILogger<WebSocketController> logger)
    {
        _eventService = eventService;
        _relayService = relayService;
        _logger = logger;
    }
    
    public async Task HandleConnection(WebSocket socket)
    {
        // Authentication logic here
        
        await ProcessMessages(socket);
    }
    
    private async Task ProcessMessages(WebSocket socket)
    {
        var buffer = new byte[4096];
        var receiveResult = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
            
        while (!receiveResult.CloseStatus.HasValue)
        {
            try
            {
                var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.TryGetProperty("event_type", out var typeElement))
                {
                    var eventType = typeElement.GetString();
                    
                    // Process and store event
                    var eventId = await _eventService.ProcessEvent(eventType, root);
                    
                    // Relay to destination service
                    await _relayService.RelayEvent(eventType, root);
                }
                else
                {
                    _logger.LogWarning("Received message without event_type property");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message");
            }
            
            receiveResult = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        
        await socket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }
}
```

## Application Startup & Registration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Core registrations
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
    
    // Register event system components
    services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
    services.AddSingleton<IEventProcessorRegistry, EventProcessorRegistry>();
    services.AddSingleton<IEventFactory, EventFactory>();
    
    // Register repositories
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped<IMessageRepository, MessageRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IGuildRepository, GuildRepository>();
    // Add more repositories as needed
    
    // Register event processors
    services.AddScoped<IEventProcessor, MessageCreateProcessor>();
    services.AddScoped<IEventProcessor, MessageUpdateProcessor>();
    services.AddScoped<IEventProcessor, MessageDeleteProcessor>();
    services.AddScoped<IEventProcessor, UserCreateProcessor>();
    services.AddScoped<IEventProcessor, UserUpdateProcessor>();
    services.AddScoped<IEventProcessor, GuildUpdateProcessor>();
    // Add more processors as needed
    
    // Register services
    services.AddScoped<IEventService, EventService>();
    services.AddScoped<IRelayService, RelayService>();
    services.AddScoped<IQueryService, QueryService>();
    
    // Web infrastructure
    services.AddSingleton<WebSocketManager>();
    services.AddHttpClient();
    
    services.AddControllers();
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env, 
    IEventTypeRegistry eventTypeRegistry, IEventProcessorRegistry processorRegistry,
    IServiceProvider serviceProvider)
{
    // Register event types
    eventTypeRegistry.RegisterEventType<MessageEvent>("MessageCreate");
    eventTypeRegistry.RegisterEventType<MessageEvent>("MessageUpdate");
    eventTypeRegistry.RegisterEventType<MessageEvent>("MessageDelete");
    eventTypeRegistry.RegisterEventType<UserEvent>("UserCreate");
    eventTypeRegistry.RegisterEventType<UserEvent>("UserUpdate");
    eventTypeRegistry.RegisterEventType<UserEvent>("UserDelete");
    eventTypeRegistry.RegisterEventType<GuildEvent>("GuildUpdate");
    eventTypeRegistry.RegisterEventType<GuildEvent>("GuildCreate");
    // Register more event types as needed
    
    // Register processors from DI
    foreach (var processor in serviceProvider.GetServices<IEventProcessor>())
    {
        processorRegistry.RegisterProcessor(processor);
    }
    
    // Standard middleware setup
    app.UseWebSockets();
    // ...
}
```

## Key Benefits of This Design

1. **Open/Closed Principle**: Add new event types without modifying existing code
   - Register new event types in startup
   - Create new processors for new events
   - No change to core event handling logic

2. **Easy Extensibility**:
   - Adding a new event type requires:
     - A new event class that extends BaseEvent
     - A processor implementation
     - Registration in startup

3. **Type Safety**: Maintains strong typing throughout the system

4. **Loose Coupling**: Components interact through interfaces

5. **Domain-Specific Repositories**: Specialized repositories for each event type provide relevant query methods

6. **Testability**: All components depend on abstractions that can be mocked

7. **Separation of Concerns**: Clear responsibilities for:
   - Event creation (Factory)
   - Event registration (Registry)
   - Event processing (Processors)
   - Event storage (Repositories)

This architecture provides a clean, maintainable framework that can scale to handle dozens or hundreds of different event types without becoming unwieldy.
---


Does this change your solution? Please provide all the needed code to implement such a solution in a way that allows for things to be extended, added, and testable. It should be robust and well structured for future additions


GitHub Copilot: # Complete WebSocket Event Relay Server Implementation

I'll provide a comprehensive implementation that combines the message format requirements with the inheritance-based event model we designed earlier. This solution builds on my previous ideas but is structured specifically for your WebSocket message format.

## Table of Contents

1. Message Format & Models
2. WebSocket Communication Components
3. Message Handling System
4. Database Integration
5. API Controllers
6. Dependency Injection & Application Setup
7. Extension Points & Testability

## 1. Message Format & Models

### WebSocket Message Contracts

```csharp
// WebSocketEventRelay/Models/WebSocket/WebSocketMessage.cs
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocketEventRelay.Models.WebSocket
{
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
    }
}
```

## 2. WebSocket Communication Components

### WebSocket Connection Class

```csharp
// WebSocketEventRelay/Models/WebSocket/WebSocketConnection.cs
using System;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace WebSocketEventRelay.Models.WebSocket
{
    public class WebSocketConnection
    {
        public string ConnectionId { get; set; } = Guid.NewGuid().ToString();
        public WebSocket Socket { get; set; } = null!;
        public string? ClientId { get; set; }
        public string? ClientType { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
```

### WebSocket Manager

```csharp
// WebSocketEventRelay/Services/WebSocket/WebSocketManager.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services.WebSocket
{
    public class WebSocketManager : IWebSocketManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _sockets = new();
        private readonly IMessageSerializationService _serializationService;
        private readonly ILogger<WebSocketManager> _logger;
        
        public WebSocketManager(
            IMessageSerializationService serializationService,
            ILogger<WebSocketManager> logger)
        {
            _serializationService = serializationService;
            _logger = logger;
        }
        
        public Task<WebSocketConnection> AddSocketAsync(WebSocket socket, string? clientId = null, string? clientType = null)
        {
            var connection = new WebSocketConnection
            {
                Socket = socket,
                ClientId = clientId,
                ClientType = clientType
            };
            
            _sockets.TryAdd(connection.ConnectionId, connection);
            _logger.LogInformation($"Socket added: {connection.ConnectionId}, Client: {clientId ?? "anonymous"}");
            
            return Task.FromResult(connection);
        }
        
        public Task RemoveSocketAsync(string connectionId)
        {
            if (_sockets.TryRemove(connectionId, out var connection))
            {
                _logger.LogInformation($"Socket removed: {connection.ConnectionId}");
            }
            
            return Task.CompletedTask;
        }
        
        public async Task SendToSocketAsync(string connectionId, object data)
        {
            if (_sockets.TryGetValue(connectionId, out var connection))
            {
                if (connection.Socket.State == WebSocketState.Open)
                {
                    var serialized = _serializationService.Serialize(data);
                    var buffer = Encoding.UTF8.GetBytes(serialized);
                    await connection.Socket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, buffer.Length),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                else
                {
                    _logger.LogWarning($"Attempted to send to a non-open socket: {connectionId}");
                }
            }
            else
            {
                _logger.LogWarning($"Attempted to send to a non-existent socket: {connectionId}");
            }
        }
        
        public async Task SendToAllAsync(object data)
        {
            var serialized = _serializationService.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(serialized);
            var tasks = _sockets.Values
                .Where(s => s.Socket.State == WebSocketState.Open)
                .Select(s => s.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None));
                    
            await Task.WhenAll(tasks);
        }
        
        public async Task SendToIssuerAsync(string issuer, object data)
        {
            var serialized = _serializationService.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(serialized);
            var tasks = _sockets.Values
                .Where(s => s.Socket.State == WebSocketState.Open && 
                       s.ClientType == issuer)
                .Select(s => s.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None));
                    
            await Task.WhenAll(tasks);
        }
        
        public IEnumerable<WebSocketConnection> GetAllSockets()
        {
            return _sockets.Values;
        }
    }
}
```

### WebSocket Manager Interface

```csharp
// WebSocketEventRelay/Services/Interfaces/IWebSocketManager.cs
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IWebSocketManager
    {
        Task<WebSocketConnection> AddSocketAsync(WebSocket socket, string? clientId = null, string? clientType = null);
        Task RemoveSocketAsync(string connectionId);
        Task SendToSocketAsync(string connectionId, object data);
        Task SendToAllAsync(object data);
        Task SendToIssuerAsync(string issuer, object data);
        IEnumerable<WebSocketConnection> GetAllSockets();
    }
}
```

## 3. Message Handling System

### Message Serialization

```csharp
// WebSocketEventRelay/Services/WebSocket/MessageSerializationService.cs
using System.Text.Json;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services.WebSocket
{
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
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IMessageSerializationService.cs
using System.Text.Json;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IMessageSerializationService
    {
        string Serialize<T>(T message);
        T? Deserialize<T>(string json);
        WebSocketMessage? DeserializeMessage(string json);
        TData? DeserializeData<TData>(JsonElement dataElement);
    }
}
```

### Message Handler System

```csharp
// WebSocketEventRelay/Services/Interfaces/IMessageHandler.cs
using System.Net.WebSockets;
using System.Threading.Tasks;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IMessageHandler
    {
        string MessageType { get; }
        Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection);
    }
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IMessageHandlerRegistry.cs
namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IMessageHandlerRegistry
    {
        void RegisterHandler(IMessageHandler handler);
        IMessageHandler GetHandler(string messageType);
        bool HasHandler(string messageType);
    }
}
```

```csharp
// WebSocketEventRelay/Services/WebSocket/MessageHandlerRegistry.cs
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services.WebSocket
{
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
}
```

### WebSocket Response Builder

```csharp
// WebSocketEventRelay/Services/WebSocket/WebSocketResponseBuilder.cs
using System;
using System.Text.Json;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.WebSocket
{
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
}
```

### WebSocket Controller

```csharp
// WebSocketEventRelay/Controllers/WebSocket/WebSocketController.cs
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Controllers.WebSocket
{
    public class WebSocketController
    {
        private readonly IWebSocketManager _webSocketManager;
        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IMessageSerializationService _serializationService;
        private readonly ILogger<WebSocketController> _logger;
        
        public WebSocketController(
            IWebSocketManager webSocketManager,
            IMessageHandlerRegistry messageHandlerRegistry,
            IMessageSerializationService serializationService,
            ILogger<WebSocketController> logger)
        {
            _webSocketManager = webSocketManager;
            _messageHandlerRegistry = messageHandlerRegistry;
            _serializationService = serializationService;
            _logger = logger;
        }
        
        public async Task HandleConnection(WebSocket socket, HttpContext context)
        {
            // Extract client info from headers or query string
            string? clientId = context.Request.Query["clientId"];
            string? clientType = context.Request.Query["clientType"];
            
            // Store connection in manager
            var connection = await _webSocketManager.AddSocketAsync(socket, clientId, clientType);
            
            // Process messages in a loop
            var buffer = new byte[4096];
            WebSocketReceiveResult? result = null;
            
            try
            {
                do
                {
                    using var ms = new MemoryStream();
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using var reader = new StreamReader(ms, Encoding.UTF8);
                        var message = await reader.ReadToEndAsync();
                        await ProcessMessage(message, connection);
                    }
                }
                while (socket.State == WebSocketState.Open && result?.MessageType != WebSocketMessageType.Close);
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection");
            }
            finally
            {
                await _webSocketManager.RemoveSocketAsync(connection.ConnectionId);
                
                if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error closing WebSocket connection");
                    }
                }
            }
        }
        
        private async Task ProcessMessage(string message, WebSocketConnection connection)
        {
            try
            {
                var wsMessage = _serializationService.DeserializeMessage(message);
                if (wsMessage == null)
                {
                    _logger.LogWarning("Received invalid message format");
                    await SendErrorResponse(connection, "Invalid message format");
                    return;
                }
                
                _logger.LogInformation($"Received message: Type={wsMessage.Type}, Id={wsMessage.Id}, Issuer={wsMessage.Issuer}");
                
                if (_messageHandlerRegistry.HasHandler(wsMessage.Type))
                {
                    var handler = _messageHandlerRegistry.GetHandler(wsMessage.Type);
                    await handler.HandleMessageAsync(wsMessage, connection);
                }
                else
                {
                    _logger.LogWarning($"No handler registered for message type: {wsMessage.Type}");
                    await SendErrorResponse(connection, $"Unknown message type: {wsMessage.Type}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message");
                await SendErrorResponse(connection, "Internal server error");
            }
        }
        
        private async Task SendErrorResponse(WebSocketConnection connection, string errorMessage)
        {
            var response = WebSocketResponseBuilder.CreateErrorResponse("unknown", errorMessage);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
    }
}
```

## 4. Specific Message Handlers

### Message Create Handler

```csharp
// WebSocketEventRelay/Services/MessageHandlers/MessageCreateHandler.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Services.MessageHandlers
{
    public class MessageCreateHandler : IMessageHandler
    {
        private readonly IMessageSerializationService _serializationService;
        private readonly IEventService _eventService;
        private readonly IRelayService _relayService;
        private readonly IWebSocketManager _webSocketManager;
        private readonly ILogger<MessageCreateHandler> _logger;
        
        public string MessageType => Models.WebSocket.MessageType.MessageCreate;
        
        public MessageCreateHandler(
            IMessageSerializationService serializationService,
            IEventService eventService,
            IRelayService relayService,
            IWebSocketManager webSocketManager,
            ILogger<MessageCreateHandler> logger)
        {
            _serializationService = serializationService;
            _eventService = eventService;
            _relayService = relayService;
            _webSocketManager = webSocketManager;
            _logger = logger;
        }
        
        public async Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection)
        {
            try
            {
                // Deserialize the specific data for this message type
                var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
                if (messageData == null)
                {
                    _logger.LogWarning("Invalid message data format");
                    await SendErrorResponse(connection, message.Id, "Invalid message data format");
                    return;
                }
                
                // Set event base properties from the WebSocket message
                messageData.EventType = MessageType;
                messageData.Timestamp = message.Timestamp;
                messageData.ServerId = messageData.ServerId ?? "";
                messageData.ChannelId = messageData.ChannelId ?? "";
                messageData.UserId = messageData.UserId ?? "";
                messageData.CreatedAt = DateTime.UtcNow;
                messageData.UpdatedAt = DateTime.UtcNow;
                
                // Process and store the event
                var eventId = await _eventService.ProcessEvent(messageData);
                _logger.LogInformation($"Created message event with ID: {eventId}");
                
                // Relay to other connected clients
                await _relayService.RelayEvent(messageData);
                
                // Send success response
                await SendSuccessResponse(connection, message.Id, new { EventId = eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message create event");
                await SendErrorResponse(connection, message.Id, "Error processing message create event");
            }
        }
        
        private async Task SendErrorResponse(WebSocketConnection connection, string messageId, string errorMessage)
        {
            var response = WebSocketResponseBuilder.CreateErrorResponse(messageId, errorMessage);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
        
        private async Task SendSuccessResponse(WebSocketConnection connection, string messageId, object data)
        {
            var response = WebSocketResponseBuilder.CreateSuccessResponse(messageId, data);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
    }
}
```

### Message Update Handler

```csharp
// WebSocketEventRelay/Services/MessageHandlers/MessageUpdateHandler.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Repositories.Interfaces;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Services.MessageHandlers
{
    public class MessageUpdateHandler : IMessageHandler
    {
        private readonly IMessageSerializationService _serializationService;
        private readonly IEventService _eventService;
        private readonly IRelayService _relayService;
        private readonly IWebSocketManager _webSocketManager;
        private readonly IMessageRepository _messageRepository;
        private readonly ILogger<MessageUpdateHandler> _logger;
        
        public string MessageType => Models.WebSocket.MessageType.MessageUpdate;
        
        public MessageUpdateHandler(
            IMessageSerializationService serializationService,
            IEventService eventService,
            IRelayService relayService,
            IWebSocketManager webSocketManager,
            IMessageRepository messageRepository,
            ILogger<MessageUpdateHandler> logger)
        {
            _serializationService = serializationService;
            _eventService = eventService;
            _relayService = relayService;
            _webSocketManager = webSocketManager;
            _messageRepository = messageRepository;
            _logger = logger;
        }
        
        public async Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection)
        {
            try
            {
                // Deserialize the specific data for this message type
                var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
                if (messageData == null)
                {
                    _logger.LogWarning("Invalid message data format");
                    await SendErrorResponse(connection, message.Id, "Invalid message data format");
                    return;
                }
                
                // Find original message
                var originalMessage = await _messageRepository.GetByMessageId(messageData.MessageId);
                if (originalMessage == null)
                {
                    _logger.LogWarning($"Original message not found: {messageData.MessageId}");
                    await SendErrorResponse(connection, message.Id, $"Original message not found: {messageData.MessageId}");
                    return;
                }
                
                // Create an update event
                var updateEvent = new MessageEvent
                {
                    EventType = MessageType,
                    MessageId = $"{originalMessage.MessageId}-edit-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                    OriginalMessageId = originalMessage.MessageId,
                    Content = messageData.Content,
                    Mentions = messageData.Mentions,
                    Attachments = messageData.Attachments,
                    Timestamp = message.Timestamp,
                    ServerId = originalMessage.ServerId,
                    ChannelId = originalMessage.ChannelId,
                    UserId = messageData.UserId ?? originalMessage.UserId,
                    IsEdited = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                // Process and store the event
                var eventId = await _eventService.ProcessEvent(updateEvent);
                _logger.LogInformation($"Created message update event with ID: {eventId}");
                
                // Relay to other connected clients
                await _relayService.RelayEvent(updateEvent);
                
                // Send success response
                await SendSuccessResponse(connection, message.Id, new { EventId = eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message update event");
                await SendErrorResponse(connection, message.Id, "Error processing message update event");
            }
        }
        
        private async Task SendErrorResponse(WebSocketConnection connection, string messageId, string errorMessage)
        {
            var response = WebSocketResponseBuilder.CreateErrorResponse(messageId, errorMessage);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
        
        private async Task SendSuccessResponse(WebSocketConnection connection, string messageId, object data)
        {
            var response = WebSocketResponseBuilder.CreateSuccessResponse(messageId, data);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
    }
}
```

## 5. Core Service Implementations

### Event Service

```csharp
// WebSocketEventRelay/Services/EventService.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services
{
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
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IEventService.cs
using System.Threading.Tasks;
using WebSocketEventRelay.Models.Events;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IEventService
    {
        Task<int> ProcessEvent(BaseEvent eventData);
    }
}
```

### Relay Service

```csharp
// WebSocketEventRelay/Services/RelayService.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebSocketEventRelay.Models.Configuration;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Services
{
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
            
            // Also relay via HTTP if configured
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
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IRelayService.cs
using System.Threading.Tasks;
using WebSocketEventRelay.Models.Events;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IRelayService
    {
        Task RelayEvent(BaseEvent eventData);
    }
}
```

```csharp
// WebSocketEventRelay/Models/Configuration/RelayConfig.cs
namespace WebSocketEventRelay.Models.Configuration
{
    public class RelayConfig
    {
        public bool WebSocketRelay { get; set; } = true;
        public bool HttpRelay { get; set; } = false;
        public string? HttpRelayEndpoint { get; set; }
    }
}
```

## 6. Repository Implementations

### Base Repository

```csharp
// WebSocketEventRelay/Repositories/Repository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebSocketEventRelay.Data;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Repositories
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly ApplicationDbContext _dbContext;
        protected readonly DbSet<TEntity> _dbSet;
        
        public Repository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = dbContext.Set<TEntity>();
        }
        
        public virtual async Task<int> Create(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Get the ID property
            var idProperty = entity.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                return (int)idProperty.GetValue(entity);
            }
            
            return 0;
        }
        
        public virtual async Task<TEntity?> GetById(int id)
        {
            return await _dbSet.FindAsync(id);
        }
        
        public virtual async Task<IEnumerable<TEntity>> GetAll()
        {
            return await _dbSet.ToListAsync();
        }
        
        public virtual async Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }
        
        public virtual async Task<TEntity?> FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }
        
        public virtual async Task Update(TEntity entity)
        {
            _dbContext.Entry(entity).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }
        
        public virtual async Task Delete(TEntity entity)
        {
            _dbSet.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
        
        public virtual async Task DeleteById(int id)
        {
            var entity = await GetById(id);
            if (entity != null)
            {
                await Delete(entity);
            }
        }
        
        public virtual async Task<int> Count()
        {
            return await _dbSet.CountAsync();
        }
        
        public virtual async Task<int> Count(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }
        
        public virtual async Task<bool> Any(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }
    }
}
```

```csharp
// WebSocketEventRelay/Repositories/Interfaces/IRepository.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace WebSocketEventRelay.Repositories.Interfaces
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task<int> Create(TEntity entity);
        Task<TEntity?> GetById(int id);
        Task<IEnumerable<TEntity>> GetAll();
        Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity?> FirstOrDefault(Expression<Func<TEntity, bool>> predicate);
        Task Update(TEntity entity);
        Task Delete(TEntity entity);
        Task DeleteById(int id);
        Task<int> Count();
        Task<int> Count(Expression<Func<TEntity, bool>> predicate);
        Task<bool> Any(Expression<Func<TEntity, bool>> predicate);
    }
}
```

### Message Repository

```csharp
// WebSocketEventRelay/Repositories/MessageRepository.cs
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebSocketEventRelay.Data;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Repositories
{
    public class MessageRepository : Repository<MessageEvent>, IMessageRepository
    {
        public MessageRepository(ApplicationDbContext dbContext) : base(dbContext) { }
        
        public async Task<MessageEvent?> GetByMessageId(string messageId)
        {
            return await _dbSet.FirstOrDefaultAsync(m => m.MessageId == messageId);
        }
        
        public async Task<MessageEvent?> GetLatestVersionOfMessage(string originalMessageId)
        {
            return await _dbSet
                .Where(m => m.OriginalMessageId == originalMessageId || m.MessageId == originalMessageId)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();
        }
        
        public async Task<IEnumerable<MessageEvent>> GetMessageHistory(string originalMessageId)
        {
            return await _dbSet
                .Where(m => m.OriginalMessageId == originalMessageId || m.MessageId == originalMessageId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }
    }
}
```

```csharp
// WebSocketEventRelay/Repositories/Interfaces/IMessageRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using WebSocketEventRelay.Models.Events;

namespace WebSocketEventRelay.Repositories.Interfaces
{
    public interface IMessageRepository : IRepository<MessageEvent>
    {
        Task<MessageEvent?> GetByMessageId(string messageId);
        Task<MessageEvent?> GetLatestVersionOfMessage(string originalMessageId);
        Task<IEnumerable<MessageEvent>> GetMessageHistory(string originalMessageId);
    }
}
```

## 7. API Controllers

```csharp
// WebSocketEventRelay/Controllers/Api/EventApiController.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Dto;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventApiController : ControllerBase
    {
        private readonly IRepository<BaseEvent> _eventRepository;
        private readonly ILogger<EventApiController> _logger;
        
        public EventApiController(
            IRepository<BaseEvent> eventRepository,
            ILogger<EventApiController> logger)
        {
            _eventRepository = eventRepository;
            _logger = logger;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnum// WebSocketEventRelay/Controllers/Api/EventApiController.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Dto;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventApiController : ControllerBase
    {
        private readonly IRepository<BaseEvent> _eventRepository;
        private readonly ILogger<EventApiController> _logger;
        
        public EventApiController(
            IRepository<BaseEvent> eventRepository,
            ILogger<EventApiController> logger)
        {
            _eventRepository = eventRepository;
            _logger = logger;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnum

CAG-nolan: Continue with your previous response (which is seen below):
```
# Complete WebSocket Event Relay Server Implementation

I'll provide a comprehensive implementation that combines the message format requirements with the inheritance-based event model we designed earlier. This solution builds on my previous ideas but is structured specifically for your WebSocket message format.

## Table of Contents

1. Message Format & Models
2. WebSocket Communication Components
3. Message Handling System
4. Database Integration
5. API Controllers
6. Dependency Injection & Application Setup
7. Extension Points & Testability

## 1. Message Format & Models

### WebSocket Message Contracts

```csharp
// WebSocketEventRelay/Models/WebSocket/WebSocketMessage.cs
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocketEventRelay.Models.WebSocket
{
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
    }
}
```

## 2. WebSocket Communication Components

### WebSocket Connection Class

```csharp
// WebSocketEventRelay/Models/WebSocket/WebSocketConnection.cs
using System;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace WebSocketEventRelay.Models.WebSocket
{
    public class WebSocketConnection
    {
        public string ConnectionId { get; set; } = Guid.NewGuid().ToString();
        public WebSocket Socket { get; set; } = null!;
        public string? ClientId { get; set; }
        public string? ClientType { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
```

### WebSocket Manager

```csharp
// WebSocketEventRelay/Services/WebSocket/WebSocketManager.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services.WebSocket
{
    public class WebSocketManager : IWebSocketManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _sockets = new();
        private readonly IMessageSerializationService _serializationService;
        private readonly ILogger<WebSocketManager> _logger;
        
        public WebSocketManager(
            IMessageSerializationService serializationService,
            ILogger<WebSocketManager> logger)
        {
            _serializationService = serializationService;
            _logger = logger;
        }
        
        public Task<WebSocketConnection> AddSocketAsync(WebSocket socket, string? clientId = null, string? clientType = null)
        {
            var connection = new WebSocketConnection
            {
                Socket = socket,
                ClientId = clientId,
                ClientType = clientType
            };
            
            _sockets.TryAdd(connection.ConnectionId, connection);
            _logger.LogInformation($"Socket added: {connection.ConnectionId}, Client: {clientId ?? "anonymous"}");
            
            return Task.FromResult(connection);
        }
        
        public Task RemoveSocketAsync(string connectionId)
        {
            if (_sockets.TryRemove(connectionId, out var connection))
            {
                _logger.LogInformation($"Socket removed: {connection.ConnectionId}");
            }
            
            return Task.CompletedTask;
        }
        
        public async Task SendToSocketAsync(string connectionId, object data)
        {
            if (_sockets.TryGetValue(connectionId, out var connection))
            {
                if (connection.Socket.State == WebSocketState.Open)
                {
                    var serialized = _serializationService.Serialize(data);
                    var buffer = Encoding.UTF8.GetBytes(serialized);
                    await connection.Socket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, buffer.Length),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                else
                {
                    _logger.LogWarning($"Attempted to send to a non-open socket: {connectionId}");
                }
            }
            else
            {
                _logger.LogWarning($"Attempted to send to a non-existent socket: {connectionId}");
            }
        }
        
        public async Task SendToAllAsync(object data)
        {
            var serialized = _serializationService.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(serialized);
            var tasks = _sockets.Values
                .Where(s => s.Socket.State == WebSocketState.Open)
                .Select(s => s.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None));
                    
            await Task.WhenAll(tasks);
        }
        
        public async Task SendToIssuerAsync(string issuer, object data)
        {
            var serialized = _serializationService.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(serialized);
            var tasks = _sockets.Values
                .Where(s => s.Socket.State == WebSocketState.Open && 
                       s.ClientType == issuer)
                .Select(s => s.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None));
                    
            await Task.WhenAll(tasks);
        }
        
        public IEnumerable<WebSocketConnection> GetAllSockets()
        {
            return _sockets.Values;
        }
    }
}
```

### WebSocket Manager Interface

```csharp
// WebSocketEventRelay/Services/Interfaces/IWebSocketManager.cs
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IWebSocketManager
    {
        Task<WebSocketConnection> AddSocketAsync(WebSocket socket, string? clientId = null, string? clientType = null);
        Task RemoveSocketAsync(string connectionId);
        Task SendToSocketAsync(string connectionId, object data);
        Task SendToAllAsync(object data);
        Task SendToIssuerAsync(string issuer, object data);
        IEnumerable<WebSocketConnection> GetAllSockets();
    }
}
```

## 3. Message Handling System

### Message Serialization

```csharp
// WebSocketEventRelay/Services/WebSocket/MessageSerializationService.cs
using System.Text.Json;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services.WebSocket
{
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
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IMessageSerializationService.cs
using System.Text.Json;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IMessageSerializationService
    {
        string Serialize<T>(T message);
        T? Deserialize<T>(string json);
        WebSocketMessage? DeserializeMessage(string json);
        TData? DeserializeData<TData>(JsonElement dataElement);
    }
}
```

### Message Handler System

```csharp
// WebSocketEventRelay/Services/Interfaces/IMessageHandler.cs
using System.Net.WebSockets;
using System.Threading.Tasks;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IMessageHandler
    {
        string MessageType { get; }
        Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection);
    }
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IMessageHandlerRegistry.cs
namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IMessageHandlerRegistry
    {
        void RegisterHandler(IMessageHandler handler);
        IMessageHandler GetHandler(string messageType);
        bool HasHandler(string messageType);
    }
}
```

```csharp
// WebSocketEventRelay/Services/WebSocket/MessageHandlerRegistry.cs
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services.WebSocket
{
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
}
```

### WebSocket Response Builder

```csharp
// WebSocketEventRelay/Services/WebSocket/WebSocketResponseBuilder.cs
using System;
using System.Text.Json;
using WebSocketEventRelay.Models.WebSocket;

namespace WebSocketEventRelay.Services.WebSocket
{
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
}
```

### WebSocket Controller

```csharp
// WebSocketEventRelay/Controllers/WebSocket/WebSocketController.cs
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Controllers.WebSocket
{
    public class WebSocketController
    {
        private readonly IWebSocketManager _webSocketManager;
        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IMessageSerializationService _serializationService;
        private readonly ILogger<WebSocketController> _logger;
        
        public WebSocketController(
            IWebSocketManager webSocketManager,
            IMessageHandlerRegistry messageHandlerRegistry,
            IMessageSerializationService serializationService,
            ILogger<WebSocketController> logger)
        {
            _webSocketManager = webSocketManager;
            _messageHandlerRegistry = messageHandlerRegistry;
            _serializationService = serializationService;
            _logger = logger;
        }
        
        public async Task HandleConnection(WebSocket socket, HttpContext context)
        {
            // Extract client info from headers or query string
            string? clientId = context.Request.Query["clientId"];
            string? clientType = context.Request.Query["clientType"];
            
            // Store connection in manager
            var connection = await _webSocketManager.AddSocketAsync(socket, clientId, clientType);
            
            // Process messages in a loop
            var buffer = new byte[4096];
            WebSocketReceiveResult? result = null;
            
            try
            {
                do
                {
                    using var ms = new MemoryStream();
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using var reader = new StreamReader(ms, Encoding.UTF8);
                        var message = await reader.ReadToEndAsync();
                        await ProcessMessage(message, connection);
                    }
                }
                while (socket.State == WebSocketState.Open && result?.MessageType != WebSocketMessageType.Close);
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection");
            }
            finally
            {
                await _webSocketManager.RemoveSocketAsync(connection.ConnectionId);
                
                if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error closing WebSocket connection");
                    }
                }
            }
        }
        
        private async Task ProcessMessage(string message, WebSocketConnection connection)
        {
            try
            {
                var wsMessage = _serializationService.DeserializeMessage(message);
                if (wsMessage == null)
                {
                    _logger.LogWarning("Received invalid message format");
                    await SendErrorResponse(connection, "Invalid message format");
                    return;
                }
                
                _logger.LogInformation($"Received message: Type={wsMessage.Type}, Id={wsMessage.Id}, Issuer={wsMessage.Issuer}");
                
                if (_messageHandlerRegistry.HasHandler(wsMessage.Type))
                {
                    var handler = _messageHandlerRegistry.GetHandler(wsMessage.Type);
                    await handler.HandleMessageAsync(wsMessage, connection);
                }
                else
                {
                    _logger.LogWarning($"No handler registered for message type: {wsMessage.Type}");
                    await SendErrorResponse(connection, $"Unknown message type: {wsMessage.Type}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message");
                await SendErrorResponse(connection, "Internal server error");
            }
        }
        
        private async Task SendErrorResponse(WebSocketConnection connection, string errorMessage)
        {
            var response = WebSocketResponseBuilder.CreateErrorResponse("unknown", errorMessage);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
    }
}
```

## 4. Specific Message Handlers

### Message Create Handler

```csharp
// WebSocketEventRelay/Services/MessageHandlers/MessageCreateHandler.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Services.MessageHandlers
{
    public class MessageCreateHandler : IMessageHandler
    {
        private readonly IMessageSerializationService _serializationService;
        private readonly IEventService _eventService;
        private readonly IRelayService _relayService;
        private readonly IWebSocketManager _webSocketManager;
        private readonly ILogger<MessageCreateHandler> _logger;
        
        public string MessageType => Models.WebSocket.MessageType.MessageCreate;
        
        public MessageCreateHandler(
            IMessageSerializationService serializationService,
            IEventService eventService,
            IRelayService relayService,
            IWebSocketManager webSocketManager,
            ILogger<MessageCreateHandler> logger)
        {
            _serializationService = serializationService;
            _eventService = eventService;
            _relayService = relayService;
            _webSocketManager = webSocketManager;
            _logger = logger;
        }
        
        public async Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection)
        {
            try
            {
                // Deserialize the specific data for this message type
                var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
                if (messageData == null)
                {
                    _logger.LogWarning("Invalid message data format");
                    await SendErrorResponse(connection, message.Id, "Invalid message data format");
                    return;
                }
                
                // Set event base properties from the WebSocket message
                messageData.EventType = MessageType;
                messageData.Timestamp = message.Timestamp;
                messageData.ServerId = messageData.ServerId ?? "";
                messageData.ChannelId = messageData.ChannelId ?? "";
                messageData.UserId = messageData.UserId ?? "";
                messageData.CreatedAt = DateTime.UtcNow;
                messageData.UpdatedAt = DateTime.UtcNow;
                
                // Process and store the event
                var eventId = await _eventService.ProcessEvent(messageData);
                _logger.LogInformation($"Created message event with ID: {eventId}");
                
                // Relay to other connected clients
                await _relayService.RelayEvent(messageData);
                
                // Send success response
                await SendSuccessResponse(connection, message.Id, new { EventId = eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message create event");
                await SendErrorResponse(connection, message.Id, "Error processing message create event");
            }
        }
        
        private async Task SendErrorResponse(WebSocketConnection connection, string messageId, string errorMessage)
        {
            var response = WebSocketResponseBuilder.CreateErrorResponse(messageId, errorMessage);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
        
        private async Task SendSuccessResponse(WebSocketConnection connection, string messageId, object data)
        {
            var response = WebSocketResponseBuilder.CreateSuccessResponse(messageId, data);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
    }
}
```

### Message Update Handler

```csharp
// WebSocketEventRelay/Services/MessageHandlers/MessageUpdateHandler.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Repositories.Interfaces;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Services.MessageHandlers
{
    public class MessageUpdateHandler : IMessageHandler
    {
        private readonly IMessageSerializationService _serializationService;
        private readonly IEventService _eventService;
        private readonly IRelayService _relayService;
        private readonly IWebSocketManager _webSocketManager;
        private readonly IMessageRepository _messageRepository;
        private readonly ILogger<MessageUpdateHandler> _logger;
        
        public string MessageType => Models.WebSocket.MessageType.MessageUpdate;
        
        public MessageUpdateHandler(
            IMessageSerializationService serializationService,
            IEventService eventService,
            IRelayService relayService,
            IWebSocketManager webSocketManager,
            IMessageRepository messageRepository,
            ILogger<MessageUpdateHandler> logger)
        {
            _serializationService = serializationService;
            _eventService = eventService;
            _relayService = relayService;
            _webSocketManager = webSocketManager;
            _messageRepository = messageRepository;
            _logger = logger;
        }
        
        public async Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection)
        {
            try
            {
                // Deserialize the specific data for this message type
                var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
                if (messageData == null)
                {
                    _logger.LogWarning("Invalid message data format");
                    await SendErrorResponse(connection, message.Id, "Invalid message data format");
                    return;
                }
                
                // Find original message
                var originalMessage = await _messageRepository.GetByMessageId(messageData.MessageId);
                if (originalMessage == null)
                {
                    _logger.LogWarning($"Original message not found: {messageData.MessageId}");
                    await SendErrorResponse(connection, message.Id, $"Original message not found: {messageData.MessageId}");
                    return;
                }
                
                // Create an update event
                var updateEvent = new MessageEvent
                {
                    EventType = MessageType,
                    MessageId = $"{originalMessage.MessageId}-edit-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                    OriginalMessageId = originalMessage.MessageId,
                    Content = messageData.Content,
                    Mentions = messageData.Mentions,
                    Attachments = messageData.Attachments,
                    Timestamp = message.Timestamp,
                    ServerId = originalMessage.ServerId,
                    ChannelId = originalMessage.ChannelId,
                    UserId = messageData.UserId ?? originalMessage.UserId,
                    IsEdited = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                // Process and store the event
                var eventId = await _eventService.ProcessEvent(updateEvent);
                _logger.LogInformation($"Created message update event with ID: {eventId}");
                
                // Relay to other connected clients
                await _relayService.RelayEvent(updateEvent);
                
                // Send success response
                await SendSuccessResponse(connection, message.Id, new { EventId = eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message update event");
                await SendErrorResponse(connection, message.Id, "Error processing message update event");
            }
        }
        
        private async Task SendErrorResponse(WebSocketConnection connection, string messageId, string errorMessage)
        {
            var response = WebSocketResponseBuilder.CreateErrorResponse(messageId, errorMessage);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
        
        private async Task SendSuccessResponse(WebSocketConnection connection, string messageId, object data)
        {
            var response = WebSocketResponseBuilder.CreateSuccessResponse(messageId, data);
            await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
        }
    }
}
```

## 5. Core Service Implementations

### Event Service

```csharp
// WebSocketEventRelay/Services/EventService.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Services
{
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
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IEventService.cs
using System.Threading.Tasks;
using WebSocketEventRelay.Models.Events;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IEventService
    {
        Task<int> ProcessEvent(BaseEvent eventData);
    }
}
```

### Relay Service

```csharp
// WebSocketEventRelay/Services/RelayService.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebSocketEventRelay.Models.Configuration;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.WebSocket;

namespace WebSocketEventRelay.Services
{
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
            
            // Also relay via HTTP if configured
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
}
```

```csharp
// WebSocketEventRelay/Services/Interfaces/IRelayService.cs
using System.Threading.Tasks;
using WebSocketEventRelay.Models.Events;

namespace WebSocketEventRelay.Services.Interfaces
{
    public interface IRelayService
    {
        Task RelayEvent(BaseEvent eventData);
    }
}
```

```csharp
// WebSocketEventRelay/Models/Configuration/RelayConfig.cs
namespace WebSocketEventRelay.Models.Configuration
{
    public class RelayConfig
    {
        public bool WebSocketRelay { get; set; } = true;
        public bool HttpRelay { get; set; } = false;
        public string? HttpRelayEndpoint { get; set; }
    }
}
```

## 6. Repository Implementations

### Base Repository

```csharp
// WebSocketEventRelay/Repositories/Repository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebSocketEventRelay.Data;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Repositories
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly ApplicationDbContext _dbContext;
        protected readonly DbSet<TEntity> _dbSet;
        
        public Repository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = dbContext.Set<TEntity>();
        }
        
        public virtual async Task<int> Create(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Get the ID property
            var idProperty = entity.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                return (int)idProperty.GetValue(entity);
            }
            
            return 0;
        }
        
        public virtual async Task<TEntity?> GetById(int id)
        {
            return await _dbSet.FindAsync(id);
        }
        
        public virtual async Task<IEnumerable<TEntity>> GetAll()
        {
            return await _dbSet.ToListAsync();
        }
        
        public virtual async Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }
        
        public virtual async Task<TEntity?> FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }
        
        public virtual async Task Update(TEntity entity)
        {
            _dbContext.Entry(entity).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }
        
        public virtual async Task Delete(TEntity entity)
        {
            _dbSet.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
        
        public virtual async Task DeleteById(int id)
        {
            var entity = await GetById(id);
            if (entity != null)
            {
                await Delete(entity);
            }
        }
        
        public virtual async Task<int> Count()
        {
            return await _dbSet.CountAsync();
        }
        
        public virtual async Task<int> Count(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }
        
        public virtual async Task<bool> Any(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }
    }
}
```

```csharp
// WebSocketEventRelay/Repositories/Interfaces/IRepository.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace WebSocketEventRelay.Repositories.Interfaces
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task<int> Create(TEntity entity);
        Task<TEntity?> GetById(int id);
        Task<IEnumerable<TEntity>> GetAll();
        Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity?> FirstOrDefault(Expression<Func<TEntity, bool>> predicate);
        Task Update(TEntity entity);
        Task Delete(TEntity entity);
        Task DeleteById(int id);
        Task<int> Count();
        Task<int> Count(Expression<Func<TEntity, bool>> predicate);
        Task<bool> Any(Expression<Func<TEntity, bool>> predicate);
    }
}
```

### Message Repository

```csharp
// WebSocketEventRelay/Repositories/MessageRepository.cs
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebSocketEventRelay.Data;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Repositories
{
    public class MessageRepository : Repository<MessageEvent>, IMessageRepository
    {
        public MessageRepository(ApplicationDbContext dbContext) : base(dbContext) { }
        
        public async Task<MessageEvent?> GetByMessageId(string messageId)
        {
            return await _dbSet.FirstOrDefaultAsync(m => m.MessageId == messageId);
        }
        
        public async Task<MessageEvent?> GetLatestVersionOfMessage(string originalMessageId)
        {
            return await _dbSet
                .Where(m => m.OriginalMessageId == originalMessageId || m.MessageId == originalMessageId)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();
        }
        
        public async Task<IEnumerable<MessageEvent>> GetMessageHistory(string originalMessageId)
        {
            return await _dbSet
                .Where(m => m.OriginalMessageId == originalMessageId || m.MessageId == originalMessageId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }
    }
}
```

```csharp
// WebSocketEventRelay/Repositories/Interfaces/IMessageRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using WebSocketEventRelay.Models.Events;

namespace WebSocketEventRelay.Repositories.Interfaces
{
    public interface IMessageRepository : IRepository<MessageEvent>
    {
        Task<MessageEvent?> GetByMessageId(string messageId);
        Task<MessageEvent?> GetLatestVersionOfMessage(string originalMessageId);
        Task<IEnumerable<MessageEvent>> GetMessageHistory(string originalMessageId);
    }
}
```

## 7. API Controllers

```csharp
// WebSocketEventRelay/Controllers/Api/EventApiController.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Dto;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventApiController : ControllerBase
    {
        private readonly IRepository<BaseEvent> _eventRepository;
        private readonly ILogger<EventApiController> _logger;
        
        public EventApiController(
            IRepository<BaseEvent> eventRepository,
            ILogger<EventApiController> logger)
        {
            _eventRepository = eventRepository;
            _logger = logger;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnum// WebSocketEventRelay/Controllers/Api/EventApiController.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.Dto;
using WebSocketEventRelay.Models.Events;
using WebSocketEventRelay.Repositories.Interfaces;

namespace WebSocketEventRelay.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventApiController : ControllerBase
    {
        private readonly IRepository<BaseEvent> _eventRepository;
        private readonly ILogger<EventApiController> _logger;
        
        public EventApiController(
            IRepository<BaseEvent> eventRepository,
            ILogger<EventApiController> logger)
        {
            _eventRepository = eventRepository;
            _logger = logger;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnum
```

GitHub Copilot: # Complete WebSocket Event Relay Implementation

I'll provide a comprehensive solution that integrates your WebSocket message format with our event models. This design follows SOLID principles and makes it easy to extend with new message types.

## Project Structure

```
WebSocketEventRelay/
├── Controllers/
│   ├── Api/
│   │   ├── EventApiController.cs
│   │   └── MessageApiController.cs
│   └── WebSocket/
│       └── WebSocketController.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Migrations/
├── Infrastructure/
│   ├── Middleware/
│   │   └── WebSocketMiddleware.cs
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs
├── Models/
│   ├── Configuration/
│   │   └── RelayConfig.cs
│   ├── Dto/
│   │   ├── EventQueryParameters.cs
│   │   └── MessageDto.cs
│   ├── Events/
│   │   ├── BaseEvent.cs
│   │   ├── MessageEvent.cs
│   │   └── [Other event types]...
│   └── WebSocket/
│       └── WebSocketMessage.cs
│       └── WebSocketConnection.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IRepository.cs
│   │   └── IMessageRepository.cs
│   ├── Repository.cs
│   └── MessageRepository.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IEventService.cs
│   │   ├── IRelayService.cs
│   │   ├── IMessageHandler.cs
│   │   ├── IMessageHandlerRegistry.cs
│   │   └── IWebSocketManager.cs
│   ├── EventService.cs
│   ├── RelayService.cs
│   ├── MessageHandlers/
│   │   ├── MessageCreateHandler.cs
│   │   └── MessageUpdateHandler.cs
│   └── WebSocket/
│       ├── MessageSerializationService.cs
│       ├── WebSocketManager.cs
│       └── WebSocketResponseBuilder.cs
├── Program.cs
└── Startup.cs
```

## 1. Core WebSocket Message Models

```csharp
// Models/WebSocket/WebSocketMessage.cs
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
    public const string VoiceUpdate = "VOICE_UPDATE";
    
    // User events
    public const string UserUpdate = "USER_UPDATE";
    
    // Guild events
    public const string GuildUpdate = "GUILD_UPDATE";
    
    // Response types
    public const string Success = "SUCCESS";
    public const string Error = "ERROR";
}
```

## 2. WebSocket Connection Management

```csharp
// Services/WebSocket/WebSocketManager.cs
public class WebSocketManager : IWebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _sockets = new();
    private readonly IMessageSerializationService _serializationService;
    private readonly ILogger<WebSocketManager> _logger;
    
    public WebSocketManager(
        IMessageSerializationService serializationService,
        ILogger<WebSocketManager> logger)
    {
        _serializationService = serializationService;
        _logger = logger;
    }
    
    public Task<WebSocketConnection> AddSocketAsync(WebSocket socket, string? clientId = null, string? clientType = null)
    {
        var connection = new WebSocketConnection
        {
            Socket = socket,
            ClientId = clientId,
            ClientType = clientType
        };
        
        _sockets.TryAdd(connection.ConnectionId, connection);
        _logger.LogInformation($"Socket added: {connection.ConnectionId}, Client: {clientId ?? "anonymous"}, Type: {clientType ?? "unknown"}");
        
        return Task.FromResult(connection);
    }
    
    public Task RemoveSocketAsync(string connectionId)
    {
        if (_sockets.TryRemove(connectionId, out var connection))
        {
            _logger.LogInformation($"Socket removed: {connection.ConnectionId}");
        }
        
        return Task.CompletedTask;
    }
    
    public async Task SendToSocketAsync(string connectionId, object data)
    {
        if (_sockets.TryGetValue(connectionId, out var connection))
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                var serialized = _serializationService.Serialize(data);
                var buffer = Encoding.UTF8.GetBytes(serialized);
                await connection.Socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }
    
    public async Task SendToAllAsync(object data)
    {
        var serialized = _serializationService.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var tasks = _sockets.Values
            .Where(s => s.Socket.State == WebSocketState.Open)
            .Select(s => s.Socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None));
                
        await Task.WhenAll(tasks);
    }
    
    public async Task SendToIssuerAsync(string issuer, object data)
    {
        var serialized = _serializationService.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var tasks = _sockets.Values
            .Where(s => s.Socket.State == WebSocketState.Open && 
                   s.ClientType == issuer)
            .Select(s => s.Socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None));
                
        await Task.WhenAll(tasks);
    }
    
    public IEnumerable<WebSocketConnection> GetAllSockets()
    {
        return _sockets.Values;
    }
}
```

## 3. Message Handler System

```csharp
// Services/Interfaces/IMessageHandler.cs
public interface IMessageHandler
{
    string MessageType { get; }
    Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection);
}

// Services/WebSocket/MessageHandlerRegistry.cs
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
```

## 4. WebSocket Controller

```csharp
// Controllers/WebSocket/WebSocketController.cs
public class WebSocketController
{
    private readonly IWebSocketManager _webSocketManager;
    private readonly IMessageHandlerRegistry _messageHandlerRegistry;
    private readonly IMessageSerializationService _serializationService;
    private readonly ILogger<WebSocketController> _logger;
    
    public WebSocketController(
        IWebSocketManager webSocketManager,
        IMessageHandlerRegistry messageHandlerRegistry,
        IMessageSerializationService serializationService,
        ILogger<WebSocketController> logger)
    {
        _webSocketManager = webSocketManager;
        _messageHandlerRegistry = messageHandlerRegistry;
        _serializationService = serializationService;
        _logger = logger;
    }
    
    public async Task HandleConnection(WebSocket socket, HttpContext context)
    {
        // Extract client info from headers or query string
        string? clientId = context.Request.Query["clientId"];
        string? clientType = context.Request.Query["clientType"];
        
        // Store connection in manager
        var connection = await _webSocketManager.AddSocketAsync(socket, clientId, clientType);
        
        // Process messages in a loop
        var buffer = new byte[4096];
        WebSocketReceiveResult? result = null;
        
        try
        {
            do
            {
                using var ms = new MemoryStream();
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.Count > 0)
                    {
                        ms.Write(buffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync();
                    await ProcessMessage(message, connection);
                }
            }
            while (socket.State == WebSocketState.Open && result?.MessageType != WebSocketMessageType.Close);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error");
        }
        finally
        {
            await _webSocketManager.RemoveSocketAsync(connection.ConnectionId);
            
            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing WebSocket connection");
                }
            }
        }
    }
    
    private async Task ProcessMessage(string message, WebSocketConnection connection)
    {
        try
        {
            var wsMessage = _serializationService.DeserializeMessage(message);
            if (wsMessage == null)
            {
                _logger.LogWarning("Received invalid message format");
                await SendErrorResponse(connection, "Invalid message format");
                return;
            }
            
            _logger.LogInformation($"Received message: Type={wsMessage.Type}, Id={wsMessage.Id}, Issuer={wsMessage.Issuer}");
            
            if (_messageHandlerRegistry.HasHandler(wsMessage.Type))
            {
                var handler = _messageHandlerRegistry.GetHandler(wsMessage.Type);
                await handler.HandleMessageAsync(wsMessage, connection);
            }
            else
            {
                _logger.LogWarning($"No handler registered for message type: {wsMessage.Type}");
                await SendErrorResponse(connection, $"Unknown message type: {wsMessage.Type}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket message");
            await SendErrorResponse(connection, "Internal server error");
        }
    }
    
    private async Task SendErrorResponse(WebSocketConnection connection, string errorMessage)
    {
        var response = WebSocketResponseBuilder.CreateErrorResponse("unknown", errorMessage);
        await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
    }
}
```

## 5. Message Handlers for Different Types

```csharp
// Services/MessageHandlers/MessageCreateHandler.cs
public class MessageCreateHandler : IMessageHandler
{
    private readonly IMessageSerializationService _serializationService;
    private readonly IEventService _eventService;
    private readonly IRelayService _relayService;
    private readonly IWebSocketManager _webSocketManager;
    private readonly ILogger<MessageCreateHandler> _logger;
    
    public string MessageType => Models.WebSocket.MessageType.MessageCreate;
    
    public MessageCreateHandler(
        IMessageSerializationService serializationService,
        IEventService eventService,
        IRelayService relayService,
        IWebSocketManager webSocketManager,
        ILogger<MessageCreateHandler> logger)
    {
        _serializationService = serializationService;
        _eventService = eventService;
        _relayService = relayService;
        _webSocketManager = webSocketManager;
        _logger = logger;
    }
    
    public async Task HandleMessageAsync(WebSocketMessage message, WebSocketConnection connection)
    {
        try
        {
            // Deserialize the specific data for this message type
            var messageData = _serializationService.DeserializeData<MessageEvent>(message.Data);
            if (messageData == null)
            {
                _logger.LogWarning("Invalid message data format");
                await SendErrorResponse(connection, message.Id, "Invalid message data format");
                return;
            }
            
            // Set event properties from the WebSocket message
            messageData.EventType = MessageType;
            messageData.Timestamp = message.Timestamp;
            messageData.CreatedAt = DateTime.UtcNow;
            messageData.UpdatedAt = DateTime.UtcNow;
            
            // Process and store the event
            var eventId = await _eventService.ProcessEvent(messageData);
            
            // Relay to other services
            await _relayService.RelayEvent(messageData);
            
            // Send success response
            await SendSuccessResponse(connection, message.Id, new { EventId = eventId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message create event");
            await SendErrorResponse(connection, message.Id, "Error processing message");
        }
    }
    
    private async Task SendErrorResponse(WebSocketConnection connection, string messageId, string errorMessage)
    {
        var response = WebSocketResponseBuilder.CreateErrorResponse(messageId, errorMessage);
        await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
    }
    
    private async Task SendSuccessResponse(WebSocketConnection connection, string messageId, object data)
    {
        var response = WebSocketResponseBuilder.CreateSuccessResponse(messageId, data);
        await _webSocketManager.SendToSocketAsync(connection.ConnectionId, response);
    }
}
```

## 6. Core Services

```csharp
// Services/EventService.cs
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

// Services/RelayService.cs
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
        
        // Also relay via HTTP if configured
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
```

## 7. API Controllers

```csharp
// Controllers/Api/EventApiController.cs
[ApiController]
[Route("api/events")]
public class EventApiController : ControllerBase
{
    private readonly IRepository<BaseEvent> _eventRepository;
    private readonly ILogger<EventApiController> _logger;
    
    public EventApiController(
        IRepository<BaseEvent> eventRepository,
        ILogger<EventApiController> logger)
    {
        _eventRepository = eventRepository;
        _logger = logger;
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BaseEvent>>> GetEvents([FromQuery] EventQueryParameters parameters)
    {
        try
        {
            var query = await _eventRepository.GetAll();
            
            // Apply filters
            if (!string.IsNullOrEmpty(parameters.EventType))
            {
                query = query.Where(e => e.EventType.Equals(parameters.EventType, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrEmpty(parameters.ServerId))
            {
                query = query.Where(e => e.ServerId == parameters.ServerId);
            }
            
            if (!string.IsNullOrEmpty(parameters.ChannelId))
            {
                query = query.Where(e => e.ChannelId == parameters.ChannelId);
            }
            
            if (!string.IsNullOrEmpty(parameters.UserId))
            {
                query = query.Where(e => e.UserId == parameters.UserId);
            }
            
            if (parameters.StartDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= parameters.StartDate.Value);
            }
            
            if (parameters.EndDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= parameters.EndDate.Value);
            }
            
            // Apply pagination
            int totalCount = query.Count();
            var pagedItems = query
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToList();
                
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            
            return Ok(pagedItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events");
            return StatusCode(500, "An error occurred while retrieving events");
        }
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<BaseEvent>> GetEvent(int id)
    {
        try
        {
            var eventData = await _eventRepository.GetById(id);
            if (eventData == null)
            {
                return NotFound();
            }
            
            return Ok(eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving event");
            return StatusCode(500, "An error occurred while retrieving the event");
        }
    }
}
```

## 8. WebSocket Middleware

```csharp
// Infrastructure/Middleware/WebSocketMiddleware.cs
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    
    public WebSocketMiddleware(RequestDelegate next, ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, WebSocketController controller)
    {
        if (context.Request.Path == "/ws")
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established");
                
                await controller.HandleConnection(webSocket, context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket connection expected");
            }
        }
        else
        {
            await _next(context);
        }
    }
}

// Infrastructure/Extensions/WebSocketMiddlewareExtensions.cs
public static class WebSocketMiddlewareExtensions
{
    public static IApplicationBuilder UseWebSocketHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WebSocketMiddleware>();
    }
}
```

## 9. Dependency Injection Setup

```csharp
// Infrastructure/Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebSocketServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<RelayConfig>(configuration.GetSection("RelayConfig"));
        
        // WebSocket services
        services.AddSingleton<IMessageSerializationService, MessageSerializationService>();
        services.AddSingleton<IWebSocketManager, WebSocketManager>();
        services.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
        services.AddScoped<WebSocketController>();
        
        // Core services
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IRelayService, RelayService>();
        
        // Add HTTP client for relay service
        services.AddHttpClient();
        
        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IMessageRepository, MessageRepository>();
        
        // Register message handlers
        services.AddScoped<IMessageHandler, MessageCreateHandler>();
        services.AddScoped<IMessageHandler, MessageUpdateHandler>();
        // Add more handlers here
        
        return services;
    }
}
```

## 10. Startup Configuration

```csharp
// Startup.cs
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Add WebSocket support
        services.AddWebSockets(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromMinutes(2);
            options.ReceiveBufferSize = 4 * 1024; // 4KB
        });
        
        // Add controllers
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
        
        // Add Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebSocket Event Relay API", Version = "v1" });
        });
        
        // Database context
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
        
        // Add WebSocket services
        services.AddWebSocketServices(Configuration);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMessageHandlerRegistry handlerRegistry, IServiceProvider serviceProvider)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebSocket Event Relay API v1"));
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();
        
        // Enable WebSockets
        app.UseWebSockets();
        
        // Use WebSocket middleware
        app.UseWebSocketHandler();
        
        // Register all message handlers
        RegisterMessageHandlers(handlerRegistry, serviceProvider);
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
    
    private void RegisterMessageHandlers(IMessageHandlerRegistry handlerRegistry, IServiceProvider serviceProvider)
    {
        // Get all handlers from DI
        var handlers = serviceProvider.GetServices<IMessageHandler>();
        foreach (var handler in handlers)
        {
            handlerRegistry.RegisterHandler(handler);
        }
    }
}
```

## 11. Program.cs

```csharp
// Program.cs
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
            });
}
```

## Key SOLID Principles Applied

1. **Single Responsibility Principle**: 
   - Each handler processes only one message type
   - WebSocketManager only manages connections
   - Services focus on specific tasks (serialization, event processing, relaying)

2. **Open/Closed Principle**:
   - Adding new message types only requires new handlers without modifying existing code
   - The message handler registry allows unlimited extensibility

3. **Liskov Substitution Principle**:
   - All event types derive from BaseEvent and can be used polymorphically
   - All message handlers implement IMessageHandler and work interchangeably 

4. **Interface Segregation Principle**:
   - Small, focused interfaces (IMessageHandler, IRepository, etc.)
   - No forced dependencies on methods classes don't need

5. **Dependency Inversion Principle**:
   - All components depend on abstractions, not concrete implementations
   - Registry pattern allows runtime resolution of handlers

## Extensibility Features

1. **Adding a new event type**:
   - Create a new class inheriting from BaseEvent
   - No changes needed to existing code

2. **Adding a new message type handler**:
   - Create a new handler implementing IMessageHandler
   - Register it in DI
   - No changes to core message processing logic

3. **Alternative relay mechanisms**:
   - RelayService can be extended to support different transport protocols
   - Configuration-driven approach allows toggling features

This architecture provides a clean, maintainable system that handles your WebSocket message format while maintaining a proper event model. It's designed for easy extension and testing, with clear separation of concerns throughout.

Similar code found with 2 license types

CAG-nolan: when i try to use WebSocket, i get this error `Type name expected, but namespace name found`. An example of when this occurs is in the WebSocketController with `public async Task HandleConnection(WebSocket socket, HttpContext context)`. How can i fix this? What type should this be? Should this be the actual System.Net.WebSockets websocket?

GitHub Copilot: # Fixing WebSocket Type Resolution Error

The error `Type name expected, but namespace name found` occurs because the compiler is interpreting `WebSocket` as a namespace rather than a type. This is typically caused by missing imports or type name conflicts.

## Solution:

### 1. Add the Correct Using Statements

At the top of your WebSocketController.cs file, ensure you have the proper imports:

```csharp
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
```

### 2. Use Fully Qualified Names (if needed)

If adding the using statement doesn't resolve the issue, use the fully qualified name:

```csharp
public async Task HandleConnection(System.Net.WebSockets.WebSocket socket, HttpContext context)
{
    // Your code here
}
```

### 3. Check for Type Name Conflicts

This error often happens when you've defined your own `WebSocket` class or interface that conflicts with the .NET WebSocket type. If that's the case:

- Rename your custom WebSocket class to something like `AppWebSocket` or `WebSocketConnection`
- Or use fully qualified names everywhere to disambiguate

### 4. Complete Example

Here's an updated version of your WebSocketController with proper imports:

```csharp
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Services.Interfaces;

namespace WebSocketEventRelay.Controllers.WebSocket
{
    public class WebSocketController
    {
        private readonly IWebSocketManager _webSocketManager;
        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IMessageSerializationService _serializationService;
        private readonly ILogger<WebSocketController> _logger;
        
        public WebSocketController(
            IWebSocketManager webSocketManager,
            IMessageHandlerRegistry messageHandlerRegistry,
            IMessageSerializationService serializationService,
            ILogger<WebSocketController> logger)
        {
            _webSocketManager = webSocketManager;
            _messageHandlerRegistry = messageHandlerRegistry;
            _serializationService = serializationService;
            _logger = logger;
        }
        
        public async Task HandleConnection(System.Net.WebSockets.WebSocket socket, HttpContext context)
        {
            // Method implementation
        }
        
        // Rest of your class...
    }
}
```

### 5. WebSocketMiddleware Update

Similarly, update your WebSocketMiddleware to use the fully qualified type:

```csharp
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    
    public WebSocketMiddleware(RequestDelegate next, ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, WebSocketController controller)
    {
        if (context.Request.Path == "/ws")
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established");
                
                await controller.HandleConnection(webSocket, context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket connection expected");
            }
        }
        else
        {
            await _next(context);
        }
    }
}
```

This clarifies to the compiler that you're using the System.Net.WebSockets.WebSocket type, not a namespace or a conflicting user-defined type.

CAG-nolan: For configuring the app, how would you setup the program.cs file?

GitHub Copilot: # Program.cs Configuration for WebSocket Relay App

Here's a complete `Program.cs` file for your WebSocket Relay application using the latest .NET style:

```csharp
// Program.cs
using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using WebSocketEventRelay.Data;
using WebSocketEventRelay.Infrastructure.Middleware;
using WebSocketEventRelay.Models.Configuration;
using WebSocketEventRelay.Models.WebSocket;
using WebSocketEventRelay.Repositories;
using WebSocketEventRelay.Repositories.Interfaces;
using WebSocketEventRelay.Services;
using WebSocketEventRelay.Services.Interfaces;
using WebSocketEventRelay.Services.MessageHandlers;
using WebSocketEventRelay.Services.WebSocket;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
// ----------------------------------------------------

// 1. Add WebSocket support
builder.Services.AddWebSockets(options =>
{
    options.KeepAliveInterval = TimeSpan.FromMinutes(2);
    options.ReceiveBufferSize = 4 * 1024; // 4KB
});

// 2. Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Add controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// 4. Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "WebSocket Event Relay API", 
        Version = "v1",
        Description = "An API for relaying and storing event messages"
    });
});

// 5. Add configuration
builder.Services.Configure<RelayConfig>(
    builder.Configuration.GetSection("RelayConfig"));

// 6. Add WebSocket infrastructure
builder.Services.AddSingleton<IMessageSerializationService, MessageSerializationService>();
builder.Services.AddSingleton<IWebSocketManager, WebSocketManager>();
builder.Services.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
builder.Services.AddScoped<WebSocketController>();

// 7. Add core services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IRelayService, RelayService>();
builder.Services.AddHttpClient();

// 8. Add repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
// Add other repositories as needed

// 9. Add message handlers
builder.Services.AddScoped<IMessageHandler, MessageCreateHandler>();
builder.Services.AddScoped<IMessageHandler, MessageUpdateHandler>();
builder.Services.AddScoped<IMessageHandler, MessageDeleteHandler>();
// Add other message handlers as needed

// Build the app
// ----------------------------------------------------
var app = builder.Build();

// 10. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebSocket Event Relay API v1"));
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// 11. Enable WebSockets
app.UseWebSockets();

// 12. Register message handlers
using (var scope = app.Services.CreateScope())
{
    var handlerRegistry = scope.ServiceProvider.GetRequiredService<IMessageHandlerRegistry>();
    var handlers = scope.ServiceProvider.GetServices<IMessageHandler>();
    
    foreach (var handler in handlers)
    {
        handlerRegistry.RegisterHandler(handler);
    }
    
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Registered {count} message handlers", handlers.Count());
}

// 13. Map WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var scope = app.Services.CreateScope();
        var controller = scope.ServiceProvider.GetRequiredService<WebSocketController>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await controller.HandleConnection(webSocket, context);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket connection expected");
    }
});

// 14. Map controller endpoints
app.MapControllers();

// 15. Run the application
app.Run();
```

## Key Features of This Configuration

1. **Modern Minimal API Style**: Uses the newer .NET 6+ approach without Startup.cs
2. **Proper Service Registration**: Registers all services with appropriate lifetimes
3. **WebSocket Configuration**: Sets reasonable buffer size and keep-alive settings
4. **Message Handler Registration**: Automatically registers all handlers at startup
5. **Dedicated WebSocket Endpoint**: Maps the WebSocket endpoint to the /ws path
6. **Scoped Service Management**: Creates proper scopes when handling WebSocket connections

## appsettings.json Configuration

Add this to your appsettings.json file:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WebSocketEventRelay;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "RelayConfig": {
    "WebSocketRelay": true,
    "HttpRelay": false,
    "HttpRelayEndpoint": "https://example.com/api/events/relay"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "WebSocketEventRelay": "Debug"
    }
  },
  "AllowedHosts": "*"
}
```

## Customizing the Configuration

You can modify the Program.cs file to:

1. **Add Authentication**: Insert app.UseAuthentication() before app.UseAuthorization()
2. **Add CORS**: Insert app.UseCors() for cross-origin support
3. **Add Rate Limiting**: Configure rate limiting middleware
4. **Use Different Database**: Change UseSqlServer to UseNpgsql, UseSqlite, etc.

This setup provides a clean, maintainable way to configure your WebSocket application while following best practices for service registration and middleware configuration.