using Coworking.Domain.Enums;

namespace Coworking.Application.Memberships.Dtos;

/// <summary>Body of <c>POST /api/memberships/{id}/payment</c> (FR07, UC09).</summary>
/// <param name="Amount">Amount received in KM; must equal the membership price (BR05).</param>
/// <param name="Method">Cash or Card.</param>
public sealed record RecordPaymentRequest(decimal Amount, PaymentMethod Method);
