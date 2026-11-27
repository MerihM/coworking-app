using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Members;
using Coworking.Application.Memberships.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Memberships;

/// <summary>
/// Implements <see cref="IMembershipService"/>. The business rules live in the domain
/// (Member.PurchaseMembership, Employee.RecordPayment → Membership.Pay, BR05); this class loads
/// what they need, checks the preconditions that should give a clear error CODE (inactive type,
/// second unpaid order, paying twice) and enforces ownership for the "mine" methods.
/// </summary>
/// <param name="db">The database.</param>
/// <param name="currentUser">The logged-in member (mine) or employee (payment).</param>
/// <param name="time">The clock: purchase/payment moment and "today" for statuses.</param>
/// <param name="listValidator">Rules for the reception list.</param>
/// <param name="buyValidator">Rules for buying.</param>
/// <param name="paymentValidator">Rules for a payment.</param>
public sealed class MembershipService(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<ListMembershipsQuery> listValidator,
    IValidator<BuyMembershipRequest> buyValidator,
    IValidator<RecordPaymentRequest> paymentValidator
) : IMembershipService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MembershipResponse>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var memberId = CurrentMemberId();
        var today = Today();

        var memberships = await WithDetails(db.Memberships.AsNoTracking())
            .Where(m => m.MemberId == memberId)
            .OrderByDescending(m => m.PurchasedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        return [.. memberships.Select(m => MembershipResponse.From(m, today))];
    }

    /// <inheritdoc />
    public async Task<MembershipResponse> BuyMineAsync(
        BuyMembershipRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await buyValidator.ValidateAndThrowAsync(request, cancellationToken);

        return await PurchaseAsync(CurrentMemberId(), request.MembershipTypeId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteMineAsync(int id, CancellationToken cancellationToken = default)
    {
        var memberId = CurrentMemberId();

        // Filtering by owner in the query: someone else's membership looks exactly like a missing
        // one (404), so ids of other members' memberships reveal nothing.
        var membership =
            await db.Memberships.SingleOrDefaultAsync(m => m.Id == id && m.MemberId == memberId, cancellationToken)
            ?? throw new NotFoundException("Membership", id);

        await RemoveUnpaidAsync(membership, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<MembershipResponse>> ListAsync(
        ListMembershipsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        var today = Today();
        var memberships = WithDetails(db.Memberships.AsNoTracking());

        if (query.MemberId is { } memberId)
        {
            memberships = memberships.Where(m => m.MemberId == memberId);
        }

        // The filter uses the SAME meaning as EffectiveStatusOn: "Expired" also covers rows still
        // stored as Active whose period is over.
        memberships = query.Status switch
        {
            MembershipStatus.Unpaid => memberships.Where(m => m.Status == MembershipStatus.Unpaid),
            MembershipStatus.Active => memberships.Where(m =>
                m.Status == MembershipStatus.Active && m.EndDate > today
            ),
            MembershipStatus.Expired => memberships.Where(m =>
                m.Status == MembershipStatus.Expired || (m.Status == MembershipStatus.Active && m.EndDate <= today)
            ),
            _ => memberships,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            memberships = memberships.Where(m =>
                (m.Member.FirstName + " " + m.Member.LastName).ToLower().Contains(term) || m.Member.Email.Contains(term)
            );
        }

        // Waiting for payment: oldest first (first come, first served at the desk).
        // Everything else: newest first (history).
        var ordered =
            query.Status == MembershipStatus.Unpaid
                ? memberships.OrderBy(m => m.PurchasedAt).ThenBy(m => m.Id)
                : memberships.OrderByDescending(m => m.PurchasedAt).ThenByDescending(m => m.Id);

        var page = await ordered.ToPagedResultAsync(query, cancellationToken);
        return new PagedResult<MembershipResponse>(
            [.. page.Items.Select(m => MembershipResponse.From(m, today))],
            page.Page,
            page.PageSize,
            page.TotalCount
        );
    }

    /// <inheritdoc />
    public async Task<MembershipResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var membership =
            await WithDetails(db.Memberships.AsNoTracking()).SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Membership", id);

        return MembershipResponse.From(membership, Today());
    }

    /// <inheritdoc />
    public async Task<MembershipResponse> SellAsync(
        int memberId,
        BuyMembershipRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await buyValidator.ValidateAndThrowAsync(request, cancellationToken);

        return await PurchaseAsync(memberId, request.MembershipTypeId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MembershipResponse> RecordPaymentAsync(
        int id,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await paymentValidator.ValidateAndThrowAsync(request, cancellationToken);

        // BR05 needs ALL of the member's memberships: a renewal starts when the current one ends
        // (Member.NextMembershipStartDate). EF's identity resolution makes "membership" and the
        // same row inside member.Memberships one object.
        var membership =
            await db
                .Memberships.Include(m => m.Member)
                .ThenInclude(member => member.Memberships)
                .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Membership", id);

        // The domain would refuse this too (422 BR05); checking here gives the clearer 409 + code.
        // Two receptionists clicking at the same moment: the unique index payments.membership_id
        // lets only one INSERT through (23505 → 409 conflict.duplicate).
        if (membership.Status != MembershipStatus.Unpaid)
        {
            throw new ConflictException(
                "This membership has already been paid.",
                code: MembershipErrorCodes.AlreadyPaid
            );
        }

        var employeeId = CurrentEmployeeId();
        var employee =
            await db.Employees.SingleOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new ForbiddenException();

        // Amount ≠ price → DomainException with ruleCode BR05 → 422 (GlobalExceptionHandler).
        employee.RecordPayment(membership, request.Amount, request.Method, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var membership =
            await db.Memberships.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Membership", id);

        await RemoveUnpaidAsync(membership, cancellationToken);
    }

    /// <summary>UC03 step 4 for both the member and reception: checks, then Member.PurchaseMembership.</summary>
    private async Task<MembershipResponse> PurchaseAsync(
        int memberId,
        int membershipTypeId,
        CancellationToken cancellationToken
    )
    {
        var member =
            await db.Members.Include(m => m.Memberships).SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        // The domain throws for both of these as well, but without a code; checking first lets the
        // frontend show a precise, translated message.
        if (!member.IsActive)
        {
            throw new BusinessRuleException(
                null,
                "This member account is deactivated.",
                MemberErrorCodes.Inactive
            );
        }

        var type =
            await db.MembershipTypes.SingleOrDefaultAsync(t => t.Id == membershipTypeId, cancellationToken)
            ?? throw new NotFoundException("MembershipType", membershipTypeId);

        if (!type.IsActive)
        {
            throw new BusinessRuleException(
                null,
                "This membership type is no longer offered.",
                MembershipErrorCodes.TypeInactive
            );
        }

        if (member.Memberships.Any(m => m.Status == MembershipStatus.Unpaid))
        {
            throw new ConflictException(
                "There is already an unpaid membership waiting for payment.",
                code: MembershipErrorCodes.UnpaidExists
            );
        }

        var membership = member.PurchaseMembership(type, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(membership.Id, cancellationToken);
    }

    /// <summary>Only unpaid memberships can be deleted; a paid one is history and revenue (FR11).</summary>
    private async Task RemoveUnpaidAsync(Membership membership, CancellationToken cancellationToken)
    {
        if (membership.Status != MembershipStatus.Unpaid)
        {
            throw new BusinessRuleException(
                null,
                "A paid membership can't be deleted.",
                MembershipErrorCodes.AlreadyPaid
            );
        }

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Member, type and payment with its employee: everything <see cref="MembershipResponse.From"/> reads.</summary>
    private static IQueryable<Membership> WithDetails(IQueryable<Membership> memberships) =>
        memberships
            .Include(m => m.Member)
            .Include(m => m.MembershipType)
            .Include(m => m.Payment)
            .ThenInclude(p => p!.Employee);

    /// <summary>The MemberOnly policy guarantees a member token; this is the defensive double check.</summary>
    private int CurrentMemberId() =>
        currentUser is { UserType: UserType.Member, Id: { } id } ? id : throw new ForbiddenException();

    /// <summary>The ReceptionistOrManager policy guarantees an employee token.</summary>
    private int CurrentEmployeeId() =>
        currentUser is { UserType: UserType.Employee, Id: { } id } ? id : throw new ForbiddenException();

    private DateOnly Today() => CenterTimeZone.ToLocalDate(time.GetUtcNow());
}
