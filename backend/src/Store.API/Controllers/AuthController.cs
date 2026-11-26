using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.API.Authorization;
using Store.Application.Auth.Requests;
using Store.Application.Auth.Responses;
using Store.Application.Auth.Services;

namespace Store.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, SessionService sessions) : ControllerBase
{
    /// <summary>Creates a Customer and logs them in (FR-010–FR-012).</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, cancellationToken);
        return Created("/api/auth/me", response);
    }

    [HttpPost("login")]
    public Task<AuthResponse> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        auth.LoginAsync(request, cancellationToken);

    /// <summary>Rotates the refresh token and returns a new pair (FR-022, FR-023).</summary>
    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken) =>
        sessions.RefreshAsync(request, cancellationToken);

    /// <summary>Revokes this device's refresh token; always 204 (FR-024, FR-029).</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public Task<UserResponse> Me(CancellationToken cancellationToken) =>
        auth.GetCurrentUserAsync(User.GetUserId(), cancellationToken);
}
