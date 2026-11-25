namespace Coworking.Application.Auth.Dtos;

/// <summary>
/// Body of <c>POST /api/auth/register</c> (FR01: name, surname, e-mail, phone and password).
/// </summary>
/// <remarks>
/// Checked by <c>RegisterMemberRequestValidator</c>. The properties are non-nullable for the
/// generated TypeScript types, but a client can still send null or leave a field out – the
/// validator catches that (MVC's own "required" check is switched off in Program.cs so that all
/// messages come from one place and can be translated).
/// </remarks>
/// <param name="FirstName">First name (ime).</param>
/// <param name="LastName">Last name (prezime).</param>
/// <param name="Email">E-mail; becomes the login name.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Password">Plain-text password; hashed immediately, never stored or logged.</param>
public sealed record RegisterMemberRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password
);
