namespace Coworking.Application.Auth.Dtos;

/// <summary>Body of <c>POST /api/auth/login</c>. Members and employees use the same form.</summary>
/// <param name="Email">E-mail (case and surrounding spaces don't matter).</param>
/// <param name="Password">Plain-text password.</param>
public sealed record LoginRequest(string Email, string Password);
