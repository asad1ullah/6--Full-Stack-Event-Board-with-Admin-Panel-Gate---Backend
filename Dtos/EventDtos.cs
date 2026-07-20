using System.ComponentModel.DataAnnotations;
using EventBoard.Api.Models;

namespace EventBoard.Api.Dtos;

public class EventCreateRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required, MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    public IFormFile? Image { get; set; }
}

public class EventUpdateRequest : EventCreateRequest
{
}

public record RsvpCounts(int Yes, int Maybe, int No);

public record EventResponse(
    int Id,
    string Title,
    string? Description,
    DateTime Date,
    string Location,
    string? Category,
    string? ImageUrl,
    int UserId,
    string? OwnerName,
    RsvpCounts RsvpCounts)
{
    public static EventResponse FromEntity(Event e) => new(
        e.Id,
        e.Title,
        e.Description,
        e.Date,
        e.Location,
        e.Category,
        e.ImagePath is null ? null : $"/uploads/{e.ImagePath}",
        e.UserId,
        e.User?.Name,
        new RsvpCounts(
            e.Rsvps.Count(r => r.Status == RsvpStatus.Yes),
            e.Rsvps.Count(r => r.Status == RsvpStatus.Maybe),
            e.Rsvps.Count(r => r.Status == RsvpStatus.No)));
}
