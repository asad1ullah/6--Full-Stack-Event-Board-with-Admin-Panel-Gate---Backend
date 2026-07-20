using System.ComponentModel.DataAnnotations;

namespace EventBoard.Api.Models;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = UserRoles.User;

    public bool IsActive { get; set; } = true;

    public ICollection<Event> Events { get; set; } = new List<Event>();

    public ICollection<Rsvp> Rsvps { get; set; } = new List<Rsvp>();
}
