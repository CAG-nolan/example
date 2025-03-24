namespace Example.Services.Interfaces;

public interface IMessageHandlerRegistry
{
    void RegisterHandler(IMessageHandler handler);
    IMessageHandler GetHandler(string messageType);
    bool HasHandler(string messageType);
}
