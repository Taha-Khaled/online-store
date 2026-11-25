using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Security;
using Store.Application.Common.Validation;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Application.Auth;

/// <summary>
/// Registration, login and the current user (FR-010–FR-016, FR-021). Logs user ids and outcomes
/// only — never emails, names, passwords or tokens (FR-004).
/// </summary>
public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var registration = Normalize(request);
        await registerValidator.ValidateOrThrowAsync(registration, cancellationToken);

        if (await EmailExistsAsync(registration.Email, cancellationToken))
        {
            throw EmailAlreadyExists();
        }

        var user = new User(
            registration.FullName,
            registration.Email,
            registration.Phone,
            passwordHasher.Hash(registration.Password),
            Role.Customer,
            timeProvider.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another registration with the same email won the race; the unique index stopped it (research R5).
            if (!await EmailExistsAsync(registration.Email, cancellationToken))
            {
                throw;
            }

            throw EmailAlreadyExists();
        }

        logger.LogInformation("Customer {UserId} registered", user.Id);
        return await IssueTokensAsync(user.Id, user.FullName, user.Email, user.Role, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var login = new LoginRequest(NormalizeEmail(request.Email), request.Password ?? string.Empty);
        await loginValidator.ValidateOrThrowAsync(login, cancellationToken);

        var account = await db.Users
            .AsNoTracking()
            .Where(user => user.Email == login.Email)
            .Select(user => new Account(user.Id, user.FullName, user.Email, user.PasswordHash, user.Role))
            .SingleOrDefaultAsync(cancellationToken);

        // An unknown email still runs a full hash check, so both failures look and take the same (FR-015).
        if (!passwordHasher.Verify(account?.PasswordHash ?? string.Empty, login.Password) || account is null)
        {
            logger.LogInformation("Login failed for user {UserId}", account?.Id);
            throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        logger.LogInformation("User {UserId} logged in", account.Id);
        return await IssueTokensAsync(account.Id, account.FullName, account.Email, account.Role, cancellationToken);
    }

    public async Task<UserResponse> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.Id, candidate.FullName, candidate.Email, candidate.Role })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedException(ErrorCodes.Unauthorized, "Authentication is required.");

        return new UserResponse(user.Id, user.FullName, user.Email, user.Role.ToString());
    }

    /// <summary>Access token + a new refresh token row for this login/device (FR-021).</summary>
    private async Task<AuthResponse> IssueTokensAsync(
        int userId,
        string fullName,
        string email,
        Role role,
        CancellationToken cancellationToken)
    {
        var refreshToken = tokenService.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken(userId, refreshToken.Hash, refreshToken.CreatedAt, refreshToken.ExpiresAt));
        await db.SaveChangesAsync(cancellationToken);

        return AuthResponses.Create(tokenService, userId, fullName, email, role, refreshToken);
    }

    private Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Email == email, cancellationToken);

    private static RegisterRequest Normalize(RegisterRequest request) =>
        new(
            request.FullName?.Trim() ?? string.Empty,
            NormalizeEmail(request.Email),
            request.Phone?.Trim() ?? string.Empty,
            request.Password ?? string.Empty);

    private static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static ConflictException EmailAlreadyExists() =>
        new(ErrorCodes.EmailAlreadyExists, "An account with this email already exists.");

    private sealed record Account(int Id, string FullName, string Email, string PasswordHash, Role Role);
}
