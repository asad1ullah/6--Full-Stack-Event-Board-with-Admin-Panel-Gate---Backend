using EventBoard.Api.Models;

namespace EventBoard.Api.Repositories;

public interface IUserRepository
{
    Task AddUserAsync(User user);

    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByIdAsync(int id);

    Task<bool> UserExistsAsync(string email);
}
