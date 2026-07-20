using System.Security.Claims;
using EventBoard.Api.Data;
using EventBoard.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Controllers;

[ApiController]
[Authorize]
public class RsvpController : ControllerBase
{
    private readonly AppDbContext _context;

    public RsvpController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // POST: api/events/5/rsvp
    [HttpPost("api/events/{eventId}/rsvp")]
    public async Task<ActionResult<RsvpResponse>> Rsvp(int eventId, RsvpRequest request)
    {
        var eventItem = await _context.Events.FindAsync(eventId);
        if (eventItem is null)
        {
            return NotFound();
        }

        var existing = await _context.Rsvps
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == CurrentUserId);

        if (existing is null)
        {
            existing = new Models.Rsvp
            {
                EventId = eventId,
                UserId = CurrentUserId,
                Status = request.Status
            };
            _context.Rsvps.Add(existing);
        }
        else
        {
            existing.Status = request.Status;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new RsvpResponse(eventItem.Id, eventItem.Title, eventItem.Date, existing.Status, existing.UpdatedAt));
    }

    // GET: api/events/5/rsvp/mine
    [HttpGet("api/events/{eventId}/rsvp/mine")]
    public async Task<ActionResult<RsvpResponse?>> GetMyRsvpForEvent(int eventId)
    {
        var rsvp = await _context.Rsvps
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == CurrentUserId);

        if (rsvp is null)
        {
            return Ok(null);
        }

        return Ok(new RsvpResponse(rsvp.EventId, rsvp.Event!.Title, rsvp.Event.Date, rsvp.Status, rsvp.UpdatedAt));
    }

    // GET: api/rsvps/mine
    [HttpGet("api/rsvps/mine")]
    public async Task<ActionResult<IEnumerable<RsvpResponse>>> GetMyRsvps()
    {
        var rsvps = await _context.Rsvps
            .Include(r => r.Event)
            .Where(r => r.UserId == CurrentUserId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

        return Ok(rsvps.Select(r => new RsvpResponse(r.EventId, r.Event!.Title, r.Event.Date, r.Status, r.UpdatedAt)));
    }
}
