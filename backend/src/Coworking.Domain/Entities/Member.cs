using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// A registered user of the center (design document: Clan). Starting point for memberships
/// and reservations. Created by registration (FR01) or by the receptionist (FR08).
/// </summary>
public class Member : Entity
{
    private readonly List<Membership> _memberships = [];
    private readonly List<Reservation> _reservations = [];

    /// <summary>Used by EF Core when loading from the database.</summary>
    private Member() { }

    /// <summary>Creates a new, active member.</summary>
    /// <param name="firstName">First name (ime).</param>
    /// <param name="lastName">Last name (prezime).</param>
    /// <param name="email">E-mail, also the login name; stored trimmed and in lower case.</param>
    /// <param name="phone">Phone number (telefon).</param>
    /// <param name="passwordHash">Already HASHED password (NFR01); hashing happens in Sprint 2.</param>
    /// <param name="registeredAt">Moment of registration (datumRegistracije).</param>
    public Member(
        string firstName,
        string lastName,
        string email,
        string phone,
        string passwordHash,
        DateTimeOffset registeredAt
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        UpdateDetails(firstName, lastName, phone);
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        RegisteredAt = registeredAt.ToUniversalTime();
        IsActive = true;
    }

    /// <summary>First name (ime).</summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>Last name (prezime).</summary>
    public string LastName { get; private set; } = null!;

    /// <summary>E-mail in lower case (email). Unique (database constraint in CW-7).</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Phone number (telefon).</summary>
    public string Phone { get; private set; } = null!;

    /// <summary>Password hash, never the password itself (lozinkaHash, NFR01).</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>Registration moment in UTC (datumRegistracije).</summary>
    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>Deactivated members can't log in, buy memberships or reserve (aktivan, FR08).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Foreign key of the fixed desk assigned to this member, if any.</summary>
    public int? FixedWorkspaceId { get; private set; }

    /// <summary>Fixed desk assigned to this member (Clan – RadnoMjesto 0..1 – 0..1).</summary>
    public Workspace? FixedWorkspace { get; private set; }

    /// <summary>All memberships, including history (Clan 1 – 0..* Clanarina).</summary>
    public IReadOnlyCollection<Membership> Memberships => _memberships;

    /// <summary>All reservations (Clan 1 – 0..* Rezervacija). Queried by the service for FR06.</summary>
    public IReadOnlyCollection<Reservation> Reservations => _reservations;

    /// <summary>Changes personal data (FR08).</summary>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="phone">New phone number.</param>
    public void UpdateDetails(string firstName, string lastName, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Phone = phone.Trim();
    }

    /// <summary>
    /// Changes the login e-mail (FR08, the receptionist edits a member). Stored trimmed and in lower
    /// case, like in the constructor. Uniqueness across members AND employees is checked by the
    /// service first; the unique index on members.email is the safety net.
    /// </summary>
    /// <param name="email">The new e-mail.</param>
    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim().ToLowerInvariant();
    }

    /// <summary>Replaces the password hash (password change, Sprint 2).</summary>
    /// <param name="passwordHash">The new hash.</param>
    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    /// <summary>Deactivates the member (FR08). Data stays for history and reports.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Activates the member again.</summary>
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Buys a membership of the given type (UC03, step 4): it starts as Unpaid and becomes
    /// active only when an employee records the payment (BR05).
    /// </summary>
    /// <param name="type">Chosen membership type; must still be offered.</param>
    /// <param name="now">Current moment (purchase time).</param>
    /// <returns>The new, unpaid membership.</returns>
    public Membership PurchaseMembership(MembershipType type, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(type);
        EnsureActive();

        if (!type.IsActive)
        {
            throw new DomainException($"The membership type '{type.Name}' is no longer offered.");
        }

        var membership = new Membership(this, type, now);
        _memberships.Add(membership);
        return membership;
    }

    /// <summary>
    /// BR02: does the member have an active membership on this date?
    /// (diagram: imaAktivnuClanarinu(datum)). Memberships must be loaded.
    /// </summary>
    /// <param name="date">Local date to check (the reservation date).</param>
    public bool HasActiveMembershipOn(DateOnly date) =>
        _memberships.Any(membership => membership.IsActiveOn(date));

    /// <summary>
    /// BR05: on which date should a newly paid membership start?
    /// On the payment date, or, if the member already has an active membership that ends
    /// later, on that membership's end date (renewal, UC03 flow 3a). Memberships must be loaded.
    /// </summary>
    /// <param name="paymentDate">Local date of the payment.</param>
    public DateOnly NextMembershipStartDate(DateOnly paymentDate) =>
        _memberships
            .Where(m => m.Status == MembershipStatus.Active && m.EndDate > paymentDate)
            .Select(m => m.EndDate!.Value)
            .DefaultIfEmpty(paymentDate)
            .Max();

    /// <summary>Assigns a fixed desk to this member (only workspaces of type FixedDesk).</summary>
    /// <param name="workspace">The fixed desk. Uniqueness (one desk per member) is a DB constraint (CW-7).</param>
    public void AssignFixedWorkspace(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (workspace.Type != WorkspaceType.FixedDesk)
        {
            throw new DomainException($"Workspace {workspace.Code} is not a fixed desk.");
        }

        FixedWorkspace = workspace;
        FixedWorkspaceId = workspace.Id;
    }

    /// <summary>Removes the fixed desk assignment.</summary>
    public void RemoveFixedWorkspace()
    {
        FixedWorkspace = null;
        FixedWorkspaceId = null;
    }

    /// <summary>Throws when the member is deactivated.</summary>
    internal void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("This member account is deactivated.");
        }
    }
}
