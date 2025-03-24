namespace Example.Services.Interfaces;

using System.Threading.Tasks;
using Example.Models.Events;

public interface IEventService
{
    Task<int> ProcessEvent(BaseEvent eventData);
}
