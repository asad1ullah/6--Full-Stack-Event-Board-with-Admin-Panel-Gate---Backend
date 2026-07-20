using System.ComponentModel.DataAnnotations;
using EventBoard.Api.Models;

namespace EventBoard.Api.Dtos;

public record RsvpRequest([Required] RsvpStatus Status);

public record RsvpResponse(int EventId, string EventTitle, DateTime EventDate, RsvpStatus Status, DateTime UpdatedAt);
