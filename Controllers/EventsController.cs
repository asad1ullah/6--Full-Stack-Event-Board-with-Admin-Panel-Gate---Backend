using System.Security.Claims;
using EventBoard.Api.Data;
using EventBoard.Api.Dtos;
using EventBoard.Api.Models;
using EventBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IImageStorageService _imageStorage;

    public EventsController(AppDbContext context, IImageStorageService imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IQueryable<Event> EventsWithIncludes() =>
        _context.Events.Include(e => e.User).Include(e => e.Rsvps);

    // GET: api/Events
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetEvents()
    {
        var events = await EventsWithIncludes().OrderBy(e => e.Date).ToListAsync();
        return Ok(events.Select(EventResponse.FromEntity));
    }

    // GET: api/Events/mine
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetMyEvents()
    {
        var events = await EventsWithIncludes()
            .Where(e => e.UserId == CurrentUserId)
            .OrderBy(e => e.Date)
            .ToListAsync();
        return Ok(events.Select(EventResponse.FromEntity));
    }

    // GET: api/Events/5
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventResponse>> GetEvent(int id)
    {
        var eventItem = await EventsWithIncludes().FirstOrDefaultAsync(e => e.Id == id);
        if (eventItem == null)
        {
            return NotFound();
        }
        return Ok(EventResponse.FromEntity(eventItem));
    }

    // POST: api/Events
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<EventResponse>> PostEvent([FromForm] EventCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var eventItem = new Event
        {
            Title = request.Title,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            Category = request.Category,
            UserId = CurrentUserId
        };

        if (request.Image is not null)
        {
            eventItem.ImagePath = await _imageStorage.SaveAsync(request.Image);
        }

        _context.Events.Add(eventItem);
        await _context.SaveChangesAsync();

        var created = await EventsWithIncludes().FirstAsync(e => e.Id == eventItem.Id);
        return CreatedAtAction(nameof(GetEvent), new { id = eventItem.Id }, EventResponse.FromEntity(created));
    }

    // PUT: api/Events/5
    [HttpPut("{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<EventResponse>> PutEvent(int id, [FromForm] EventUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var eventItem = await _context.Events.FindAsync(id);
        if (eventItem == null)
        {
            return NotFound();
        }

        eventItem.Title = request.Title;
        eventItem.Description = request.Description;
        eventItem.Date = request.Date;
        eventItem.Location = request.Location;
        eventItem.Category = request.Category;

        if (request.Image is not null)
        {
            var oldImage = eventItem.ImagePath;
            eventItem.ImagePath = await _imageStorage.SaveAsync(request.Image);
            _imageStorage.Delete(oldImage);
        }

        await _context.SaveChangesAsync();

        var updated = await EventsWithIncludes().FirstAsync(e => e.Id == id);
        return Ok(EventResponse.FromEntity(updated));
    }

    // DELETE: api/Events/5
    [HttpDelete("{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var eventItem = await _context.Events.FindAsync(id);
        if (eventItem == null)
        {
            return NotFound();
        }

        _context.Events.Remove(eventItem);
        await _context.SaveChangesAsync();
        _imageStorage.Delete(eventItem.ImagePath);

        return NoContent();
    }
}
