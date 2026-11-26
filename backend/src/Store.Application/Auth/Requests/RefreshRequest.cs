namespace Store.Application.Auth.Requests;

/// <summary>Body of refresh and logout.</summary>
public sealed record RefreshRequest(string RefreshToken);
