using Example.Controllers.WebSocket;

namespace Example.Infrastructure.Middleware;

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
