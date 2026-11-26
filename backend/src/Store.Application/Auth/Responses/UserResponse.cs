namespace Store.Application.Auth.Responses;

public sealed record UserResponse(int Id, string FullName, string Email, string Role);
