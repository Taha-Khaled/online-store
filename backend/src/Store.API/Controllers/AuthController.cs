using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Store.Application.Auth;
using Store.Application.Common.Errors;

namespace Store.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
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

    [Authorize]
    [HttpGet("me")]
    public Task<UserResponse> Me(CancellationToken cancellationToken) =>
        auth.GetCurrentUserAsync(GetUserId(), cancellationToken);

    private int GetUserId() =>
        int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            ? userId
            : throw new UnauthorizedException(ErrorCodes.Unauthorized, "Authentication is required.");
}
