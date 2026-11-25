namespace Store.Application.Auth;

public sealed record UserResponse(int Id, string FullName, string Email, string Role);
