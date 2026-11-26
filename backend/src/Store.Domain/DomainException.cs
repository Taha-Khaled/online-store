namespace Store.Domain;

/// <summary>
/// Thrown when an entity rule is broken. Application services translate it into the operation's
/// error code (research R1); it never reaches the client as a 500.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
