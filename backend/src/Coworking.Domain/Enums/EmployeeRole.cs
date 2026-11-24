namespace Coworking.Domain.Enums;

/// <summary>Role of an employee; decides which functions they can use (design document: UlogaZaposlenika).</summary>
public enum EmployeeRole
{
    /// <summary>Records payments, manages members, can cancel any reservation (Recepcioner).</summary>
    Receptionist,

    /// <summary>Manages rooms, workspaces, membership types and reports (Menadzer).</summary>
    Manager,

    /// <summary>Creates staff accounts and assigns roles (Administrator).</summary>
    Administrator,
}
