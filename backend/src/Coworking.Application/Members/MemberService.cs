using Coworking.Application.Auth;
using Coworking.Application.Common;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Members.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Members;

/// <summary>
/// Implements <see cref="IMemberService"/> (FR08, UC10). Who may call it (Receptionist or Manager)
/// is decided by the controller's policy; this class validates input, keeps e-mails unique across
/// members and employees, and guards the fixed desk assignment.
/// </summary>
/// <param name="db">The database.</param>
/// <param name="passwordHasher">Hashes the initial password.</param>
/// <param name="refreshTokens">Ends sessions when a member is deactivated.</param>
/// <param name="time">The clock ("today" for the membership summary, registration time).</param>
/// <param name="listValidator">Rules for the list query.</param>
/// <param name="createValidator">Rules for a new member.</param>
/// <param name="updateValidator">Rules for an edit.</param>
public sealed class MemberService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokens,
    TimeProvider time,
    IValidator<ListMembersQuery> listValidator,
    IValidator<CreateMemberRequest> createValidator,
    IValidator<UpdateMemberRequest> updateValidator
) : IMemberService
{
    /// <inheritdoc />
    public async Task<PagedResult<MemberResponse>> ListAsync(
        ListMembersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        var members = WithDetails(db.Members.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Same approach as the staff list: lower(...) LIKE in SQL; e-mails are stored in lower
            // case already. The phone is searched as typed ("061" finds "061/123-456").
            var term = query.Search.Trim().ToLowerInvariant();
            members = members.Where(m =>
                m.FirstName.ToLower().Contains(term)
                || m.LastName.ToLower().Contains(term)
                || (m.FirstName + " " + m.LastName).ToLower().Contains(term)
                || m.Email.Contains(term)
                || m.Phone.Contains(term)
            );
        }

        if (query.IsActive is { } isActive)
        {
            members = members.Where(m => m.IsActive == isActive);
        }

        var page = await members
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .ThenBy(m => m.Id)
            .ToPagedResultAsync(query, cancellationToken);

        // Map after loading: the "current membership" summary uses the domain rule IsActiveOn.
        // A page has at most 100 members with a handful of memberships each, so this is cheap.
        var today = Today();
        return new PagedResult<MemberResponse>(
            [.. page.Items.Select(m => MemberResponse.From(m, today))],
            page.Page,
            page.PageSize,
            page.TotalCount
        );
    }

    /// <inheritdoc />
    public async Task<MemberResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var member =
            await WithDetails(db.Members.AsNoTracking()).SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Member", id);

        return MemberResponse.From(member, Today());
    }

    /// <inheritdoc />
    public async Task<MemberResponse> CreateAsync(
        CreateMemberRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = EmailAddresses.Normalize(request.Email);
        if (await db.IsEmailTakenAsync(email, cancellationToken: cancellationToken))
        {
            throw EmailTaken();
        }

        var member = new Member(
            request.FirstName,
            request.LastName,
            email,
            request.Phone,
            passwordHasher.Hash(request.Password),
            time.GetUtcNow()
        );
        await ApplyFixedDeskAsync(member, request.FixedWorkspaceId, cancellationToken);

        db.Members.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(member.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MemberResponse> UpdateAsync(
        int id,
        UpdateMemberRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var member = await FindAsync(id, cancellationToken);

        var email = EmailAddresses.Normalize(request.Email);
        if (email != member.Email)
        {
            if (await db.IsEmailTakenExceptMemberAsync(email, id, cancellationToken))
            {
                throw EmailTaken();
            }

            member.ChangeEmail(email);
        }

        member.UpdateDetails(request.FirstName, request.LastName, request.Phone);
        await ApplyFixedDeskAsync(member, request.FixedWorkspaceId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var member = await FindAsync(id, cancellationToken);
        if (!member.IsActive)
        {
            return; // Idempotent.
        }

        member.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        // Login already refuses inactive members; this also stops the next refresh, so an open
        // browser session ends within 15 minutes (the access token's lifetime).
        await refreshTokens.RevokeAllForUserAsync(UserType.Member, id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var member = await FindAsync(id, cancellationToken);
        member.Reactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var member = await FindAsync(id, cancellationToken);
        db.Members.Remove(member);

        // memberships.member_id and reservations.member_id are ON DELETE RESTRICT: a member with
        // history stays (reports, payments). PostgreSQL refuses with 23001 → 409 "conflict.in_use"
        // and the UI suggests deactivating. Refresh tokens are ON DELETE CASCADE and simply go.
        // A fixed desk needs nothing: the FK lives on the member row that is being deleted.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Fixed desk + memberships with their types: everything <see cref="MemberResponse.From"/> reads.</summary>
    private static IQueryable<Member> WithDetails(IQueryable<Member> members) =>
        members.Include(m => m.FixedWorkspace).Include(m => m.Memberships).ThenInclude(ms => ms.MembershipType);

    /// <summary>
    /// Sets or removes the fixed desk (Clan – RadnoMjesto 0..1 – 0..1). Rules: only an active
    /// FixedDesk in an active room, and not one that another member already has. The unique index
    /// on members.fixed_workspace_id is the database backstop (23505 → 409 conflict.duplicate).
    /// </summary>
    private async Task ApplyFixedDeskAsync(Member member, int? workspaceId, CancellationToken cancellationToken)
    {
        if (workspaceId is null)
        {
            member.RemoveFixedWorkspace();
            return;
        }

        if (member.FixedWorkspaceId == workspaceId)
        {
            return; // Unchanged: keep it even if the room was deactivated in the meantime.
        }

        var desk =
            await db.Workspaces.Include(w => w.Room).SingleOrDefaultAsync(w => w.Id == workspaceId, cancellationToken)
            ?? throw new NotFoundException("Workspace", workspaceId);

        if (desk.Type != WorkspaceType.FixedDesk || !desk.IsActive || !desk.Room.IsActive)
        {
            throw new BusinessRuleException(
                null,
                "Only an active fixed desk can be assigned to a member.",
                MemberErrorCodes.FixedDeskInvalid
            );
        }

        // member.Id is 0 for a new member, which matches no row – exactly what we want.
        var taken = await db.Members.AnyAsync(
            m => m.FixedWorkspaceId == desk.Id && m.Id != member.Id,
            cancellationToken
        );
        if (taken)
        {
            throw new ConflictException(
                "This fixed desk is already assigned to another member.",
                code: MemberErrorCodes.FixedDeskTaken
            );
        }

        member.AssignFixedWorkspace(desk);
    }

    /// <summary>Loads a tracked member (so changes are saved) or throws 404.</summary>
    private async Task<Member> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Members.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
        ?? throw new NotFoundException("Member", id);

    private DateOnly Today() => CenterTimeZone.ToLocalDate(time.GetUtcNow());

    private static ConflictException EmailTaken() =>
        new("An account with this e-mail address already exists.", code: AuthErrorCodes.EmailTaken);
}
