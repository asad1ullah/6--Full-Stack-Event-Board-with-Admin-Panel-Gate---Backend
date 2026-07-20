using EventBoard.Api.Dtos;
using EventBoard.Api.Models;
using EventBoard.Api.Repositories;
using EventBoard.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventBoard.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthController(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _userRepository.UserExistsAsync(request.Email))
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Role = UserRoles.User,
            IsActive = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddUserAsync(user);

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, ToUserResponse(user)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, ToUserResponse(user)));
    }

    private static UserResponse ToUserResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.Role, user.IsActive);
}
