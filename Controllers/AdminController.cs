using EventBoard.Api.Data;
using EventBoard.Api.Dtos;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserResponse>>> GetUsers()
    {
        var users = await _context.Users.OrderBy(u => u.Name).ToListAsync();
        return Ok(users.Select(u => new AdminUserResponse(u.Id, u.Name, u.Email, u.Role, u.IsActive)));
    }

    [HttpPut("users/{id}/role")]
    public async Task<ActionResult<AdminUserResponse>> UpdateRole(int id, UpdateRoleRequest request)
    {
        if (request.Role != UserRoles.Admin && request.Role != UserRoles.User)
        {
            return BadRequest(new { message = $"Role must be '{UserRoles.Admin}' or '{UserRoles.User}'." });
        }

        var user = await _context.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.Role = request.Role;
        await _context.SaveChangesAsync();

        return Ok(new AdminUserResponse(user.Id, user.Name, user.Email, user.Role, user.IsActive));
    }

    [HttpPut("users/{id}/status")]
    public async Task<ActionResult<AdminUserResponse>> UpdateStatus(int id, UpdateStatusRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new AdminUserResponse(user.Id, user.Name, user.Email, user.Role, user.IsActive));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsResponse>> GetStats()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalEvents = await _context.Events.CountAsync();
        var totalRsvps = await _context.Rsvps.CountAsync();

        var rsvpsPerEvent = await _context.Events
            .OrderBy(e => e.Date)
            .Select(e => new EventRsvpStat(
                e.Id,
                e.Title,
                e.Rsvps.Count(r => r.Status == RsvpStatus.Yes),
                e.Rsvps.Count(r => r.Status == RsvpStatus.Maybe),
                e.Rsvps.Count(r => r.Status == RsvpStatus.No)))
            .ToListAsync();

        return Ok(new AdminStatsResponse(totalUsers, totalEvents, totalRsvps, rsvpsPerEvent));
    }
}
