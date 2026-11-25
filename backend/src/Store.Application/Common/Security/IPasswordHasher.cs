namespace Store.Application.Common.Security;

/// <summary>Hashes and verifies passwords from the password alone (FR-026, research R1).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Checks a password against a stored hash. An empty hash means "no such account": the check
    /// still costs the same as a real one and returns false, so timing does not reveal which
    /// emails exist (FR-015).
    /// </summary>
    bool Verify(string passwordHash, string password);
}
