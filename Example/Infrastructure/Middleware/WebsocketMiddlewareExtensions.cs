namespace Example.Infrastructure.Middleware;

public static class WebSocketMiddlewareExtensions
{
    public static IApplicationBuilder UseWebSocketHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WebSocketMiddleware>();
    }
}