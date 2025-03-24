using Example.Controllers.WebSocket;
using Example.Models.Configuration;
using Example.Repositories;
using Example.Repositories.Interfaces;
using Example.Services;
using Example.Services.Handlers.Message;
using Example.Services.Interfaces;
using Example.Services.WebSocket;
using WebSocketManager = Example.Controllers.WebSocket.WebSocketManager;

namespace Example.Infrastructure.Extensions;

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

        // Add more handlers here
        
        return services;
    }
}