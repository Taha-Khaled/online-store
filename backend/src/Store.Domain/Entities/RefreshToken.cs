namespace Store.Domain.Entities;

/// <summary>
/// One login session on one device (data-model.md "New: RefreshToken"). Only the hash of the
/// token is stored. States: active, rotated (revoked and replaced), revoked (logout), expired.
/// </summary>
public class RefreshToken
{
    public RefreshToken(int userId, string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("A refresh token must expire after it is created.", nameof(expiresAt));
        }

        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>True once a refresh replaced this token; reusing it triggers reuse detection (FR-023).</summary>
    public bool IsRotated => ReplacedByTokenHash is not null;

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Revokes the token; keeps the first revocation time if it is already revoked.</summary>
    public void Revoke(DateTime now) => RevokedAt ??= now;

    /// <summary>Revokes an active token and records the hash of the token that replaced it (FR-022).</summary>
    public void MarkRotated(DateTime now, string replacedByHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replacedByHash);
        if (!IsActive(now))
        {
            throw new InvalidOperationException("Only an active refresh token can be rotated.");
        }

        RevokedAt = now;
        ReplacedByTokenHash = replacedByHash;
    }
}
