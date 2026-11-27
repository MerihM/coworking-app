using Coworking.Application.Common.Pagination;
using Coworking.Domain.Enums;

namespace Coworking.Application.Memberships.Dtos;

/// <summary>
/// Query string of <c>GET /api/memberships</c> (reception), e.g. <c>?status=Unpaid</c> for the
/// "waiting for payment" list or <c>?memberId=7</c> for one member's history.
/// </summary>
public sealed class ListMembershipsQuery : PageQuery
{
    /// <summary>Only this member's memberships.</summary>
    public int? MemberId { get; set; }

    /// <summary>Unpaid, Active (incl. paid renewals that start later) or Expired – as of today.</summary>
    public MembershipStatus? Status { get; set; }

    /// <summary>Part of the member's name or e-mail (case-insensitive).</summary>
    public string? Search { get; set; }
}
