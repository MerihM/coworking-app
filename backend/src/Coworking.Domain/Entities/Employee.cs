using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// A receptionist, manager or administrator (design document: Zaposlenik).
/// One class with a role instead of three classes, because they share the same data
/// and differ only in permissions (chapter 4.1). Accounts are created by the administrator (FR12).
/// </summary>
public class Employee : Entity
{
    /// <summary>Used by EF Core when loading from the database.</summary>
    private Employee() { }

    /// <summary>Creates a new, active employee account.</summary>
    /// <param name="firstName">First name.</param>
    /// <param name="lastName">Last name.</param>
    /// <param name="email">E-mail (login name), stored in lower case.</param>
    /// <param name="passwordHash">Already hashed password (NFR01).</param>
    /// <param name="role">Role that decides access rights.</param>
    public Employee(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        EmployeeRole role
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        UpdateDetails(firstName, lastName);
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
    }

    /// <summary>First name (ime).</summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>Last name (prezime).</summary>
    public string LastName { get; private set; } = null!;

    /// <summary>E-mail in lower case. Unique.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Password hash (lozinkaHash, NFR01).</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>Role (uloga).</summary>
    public EmployeeRole Role { get; private set; }

    /// <summary>
    /// Deactivated employees can't log in. Not in the Class diagram; added because accounts of
    /// former employees must stay (payments reference them) but must stop working.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Changes the name.</summary>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    public void UpdateDetails(string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    /// <summary>Assigns a different role (FR12).</summary>
    /// <param name="role">The new role.</param>
    public void ChangeRole(EmployeeRole role) => Role = role;

    /// <summary>Replaces the password hash.</summary>
    /// <param name="passwordHash">The new hash.</param>
    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    /// <summary>Deactivates the account.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Activates the account again.</summary>
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Records the payment of a membership (FR07, UC09; diagram: evidentirajUplatu).
    /// Calculates the start date by BR05 (payment date, or renewal on the existing end date)
    /// and activates the membership. The member's memberships must be loaded.
    /// </summary>
    /// <param name="membership">The unpaid membership being paid.</param>
    /// <param name="amount">Amount received; must equal the membership price.</param>
    /// <param name="method">Cash or card.</param>
    /// <param name="paidAt">Moment of payment.</param>
    /// <returns>The recorded payment.</returns>
    public Payment RecordPayment(
        Membership membership,
        decimal amount,
        PaymentMethod method,
        DateTimeOffset paidAt
    )
    {
        ArgumentNullException.ThrowIfNull(membership);

        if (!IsActive)
        {
            throw new DomainException("A deactivated employee can't record payments.");
        }

        var paymentDate = CenterTimeZone.ToLocalDate(paidAt);
        var startDate = membership.Member.NextMembershipStartDate(paymentDate);

        return membership.Pay(this, amount, method, paidAt, startDate);
    }
}
