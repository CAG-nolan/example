using Example.Models.Events;

namespace Example.Repositories.Interfaces;

public interface IMessageRepository : IRepository<MessageEvent>
{
    Task<MessageEvent?> GetByMessageId(string messageId);
}