using Microsoft.EntityFrameworkCore;
using Example.Data;
using Example.Models.Events;
using Example.Repositories.Interfaces;

namespace Example.Repositories;


public class MessageRepository : Repository<MessageEvent>, IMessageRepository
{
    public MessageRepository(ApplicationDbContext dbContext) : base(dbContext) { }
    
    public async Task<MessageEvent?> GetByMessageId(string messageId)
    {
        return await _dbSet.FirstOrDefaultAsync(m => m.MessageId == messageId);
    }
}
