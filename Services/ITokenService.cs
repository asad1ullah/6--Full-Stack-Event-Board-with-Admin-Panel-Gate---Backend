using EventBoard.Api.Models;

namespace EventBoard.Api.Services;

public interface ITokenService
{
    string CreateToken(User user);
}
