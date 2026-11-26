namespace Store.Application.Auth.Requests;

public sealed record RegisterRequest(string FullName, string Email, string Phone, string Password);
