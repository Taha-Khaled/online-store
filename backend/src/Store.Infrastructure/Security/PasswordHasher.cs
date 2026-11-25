using Microsoft.AspNetCore.Identity;
using Store.Application.Common.Security;

namespace Store.Infrastructure.Security;

/// <summary>
/// ASP.NET Core <see cref="PasswordHasher{TUser}"/> (constitution III) behind <see cref="IPasswordHasher"/>.
/// The V3 algorithm ignores the user argument, so a fixed placeholder is passed; hashes created by
/// the 001 seeder with <c>PasswordHasher&lt;User&gt;</c> verify unchanged (research R1).
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private static readonly object Placeholder = new();
    private static readonly PasswordHasher<object> Hasher = new();

    // Hash of a random value nobody knows; used to spend the same time when there is no account.
    private static readonly string UnknownAccountHash = Hasher.HashPassword(Placeholder, Guid.NewGuid().ToString("N"));

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return Hasher.HashPassword(Placeholder, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            Hasher.VerifyHashedPassword(Placeholder, UnknownAccountHash, password);
            return false;
        }

        return Hasher.VerifyHashedPassword(Placeholder, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
