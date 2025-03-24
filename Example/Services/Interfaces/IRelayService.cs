using System.Threading.Tasks;
using Example.Models.Events;

namespace Example.Services.Interfaces;

public interface IRelayService
{
    Task RelayEvent(BaseEvent eventData);
}
