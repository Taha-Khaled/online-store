namespace Store.Application.Common.Security;

/// <summary>Hashes and verifies passwords from the password alone (FR-026, research R1).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
