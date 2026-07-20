using System.ComponentModel.DataAnnotations;

namespace EventBoard.Api.Dtos;

public record RegisterRequest(
    [Required, MaxLength(100)] string Name,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(6)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record UserResponse(int Id, string Name, string Email, string Role, bool IsActive);

public record AuthResponse(string Token, UserResponse User);
