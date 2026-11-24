using Store.Domain.Enums;

namespace Store.Domain.Entities;

public class User
{
    public User(string fullName, string email, string phone, string passwordHash, Role role, DateTime createdAt)
    {
        FullName = fullName;
        Email = email;
        Phone = phone;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public string PasswordHash { get; private set; }
    public Role Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
