namespace Store.Application.Auth.Requests;

public sealed record LoginRequest(string Email, string Password);
