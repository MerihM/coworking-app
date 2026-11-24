namespace Coworking.Domain.Enums;

/// <summary>How a payment was made at the reception (design document: NacinPlacanja).</summary>
public enum PaymentMethod
{
    /// <summary>Cash (Gotovina).</summary>
    Cash,

    /// <summary>Card at the reception terminal (Kartica).</summary>
    Card,
}
