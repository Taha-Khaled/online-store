using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Security;
using Store.Application.Common.Validation;
using Store.Domain.Entities;

namespace Store.Application.Auth;

/// <summary>
/// Refresh-token rotation, reuse detection and logout (FR-022–FR-024, FR-028, FR-029; research R4).
/// Every refresh failure is the same 401 <c>invalidRefreshToken</c>. Logs token row ids and user
/// ids only — never a token or its hash (FR-004).
/// </summary>
public sealed class SessionService(
    IAppDbContext db,
    ITokenService tokenService,
    IValidator<RefreshRequest> refreshValidator,
    TimeProvider timeProvider,
    ILogger<SessionService> logger)
{
    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = await ValidateAndHashAsync(request, cancellationToken);
        var token = await db.RefreshTokens
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken)
            ?? throw InvalidRefreshToken();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Rotated is checked before expiry: a reused rotated token always ends the user's sessions (FR-023).
        if (token.IsRotated)
        {
            await RevokeAllForReuseAsync(token.Id, token.UserId, now, cancellationToken);
            throw InvalidRefreshToken();
        }

        if (!token.IsActive(now))
        {
            logger.LogInformation("Refresh rejected: token {TokenId} is expired or logged out", token.Id);
            throw InvalidRefreshToken();
        }

        var replacement = tokenService.CreateRefreshToken();
        token.MarkRotated(now, replacement.Hash);
        db.RefreshTokens.Add(new RefreshToken(token.UserId, replacement.Hash, replacement.CreatedAt, replacement.ExpiresAt));

        try
        {
            // One SaveChanges = one transaction: revoke the old token (only if still not revoked) and insert the new one.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await HandleLostRaceAsync(token.Id, token.UserId, now, cancellationToken);
            throw InvalidRefreshToken();
        }

        logger.LogInformation("Refresh token {TokenId} rotated for user {UserId}", token.Id, token.UserId);
        return AuthResponses.Create(tokenService, token.UserId, token.User.FullName, token.User.Email, token.User.Role, replacement);
    }

    /// <summary>Revokes the token if it is active; does nothing otherwise (FR-024, FR-029).</summary>
    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = await ValidateAndHashAsync(request, cancellationToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (token is null || !token.IsActive(now))
        {
            return;
        }

        token.Revoke(now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Refresh token {TokenId} revoked by logout for user {UserId}", token.Id, token.UserId);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent refresh or logout revoked it first; the token is inactive either way.
            logger.LogInformation("Refresh token {TokenId} was already revoked when logging out", token.Id);
        }
    }

    private async Task<string> ValidateAndHashAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var normalized = new RefreshRequest(request.RefreshToken ?? string.Empty);
        await refreshValidator.ValidateOrThrowAsync(normalized, cancellationToken);
        return tokenService.HashRefreshToken(normalized.RefreshToken);
    }

    /// <summary>Another request changed the token first; if it rotated it, this is a reuse (research R4).</summary>
    private async Task HandleLostRaceAsync(int tokenId, int userId, DateTime now, CancellationToken cancellationToken)
    {
        var rotatedByOther = await db.RefreshTokens
            .AsNoTracking()
            .Where(candidate => candidate.Id == tokenId)
            .Select(candidate => candidate.ReplacedByTokenHash != null)
            .SingleAsync(cancellationToken);

        if (rotatedByOther)
        {
            await RevokeAllForReuseAsync(tokenId, userId, now, cancellationToken);
        }
    }

    /// <summary>
    /// Revokes every not-yet-revoked token of the user in one statement, keeping earlier revocation
    /// times (same rule as <see cref="RefreshToken.Revoke"/>).
    /// </summary>
    private async Task RevokeAllForReuseAsync(int reusedTokenId, int userId, DateTime now, CancellationToken cancellationToken)
    {
        var revoked = await db.RefreshTokens
            .Where(candidate => candidate.UserId == userId && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.RevokedAt, now), cancellationToken);

        logger.LogWarning(
            "Reuse of rotated refresh token {TokenId}: revoked {RevokedCount} tokens of user {UserId}",
            reusedTokenId,
            revoked,
            userId);
    }

    private static UnauthorizedException InvalidRefreshToken() =>
        new(ErrorCodes.InvalidRefreshToken, "The refresh token is invalid or expired.");
}
