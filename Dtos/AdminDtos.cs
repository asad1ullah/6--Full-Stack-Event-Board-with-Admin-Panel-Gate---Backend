using System.ComponentModel.DataAnnotations;

namespace EventBoard.Api.Dtos;

public record AdminUserResponse(int Id, string Name, string Email, string Role, bool IsActive);

public record UpdateRoleRequest([Required] string Role);

public record UpdateStatusRequest([Required] bool IsActive);

public record EventRsvpStat(int EventId, string Title, int Yes, int Maybe, int No);

public record AdminStatsResponse(int TotalUsers, int TotalEvents, int TotalRsvps, IEnumerable<EventRsvpStat> RsvpsPerEvent);
