namespace Coworking.Application.MembershipTypes.Dtos;

/// <summary>The fields create and update share, so one validator class checks both requests.</summary>
public interface IMembershipTypeInput
{
    /// <summary>Name as typed.</summary>
    string Name { get; }

    /// <summary>Duration in days.</summary>
    int DurationDays { get; }

    /// <summary>Price in KM.</summary>
    decimal Price { get; }
}
