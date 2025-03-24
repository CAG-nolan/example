using Example.Models.Events;
using Example.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Example.Controllers.Api;

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