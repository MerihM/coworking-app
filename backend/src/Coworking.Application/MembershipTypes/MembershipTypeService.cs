using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.MembershipTypes.Dtos;
using Coworking.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.MembershipTypes;

/// <summary>
/// Implements <see cref="IMembershipTypeService"/>. Who may call which method (Manager for
/// management, anyone for the offer) is decided by the controller; this class validates input,
/// keeps names unique and maps to responses.
/// </summary>
/// <param name="db">The database.</param>
/// <param name="listValidator">Rules for the list query.</param>
/// <param name="createValidator">Rules for a new type.</param>
/// <param name="updateValidator">Rules for an edit.</param>
public sealed class MembershipTypeService(
    IAppDbContext db,
    IValidator<ListMembershipTypesQuery> listValidator,
    IValidator<CreateMembershipTypeRequest> createValidator,
    IValidator<UpdateMembershipTypeRequest> updateValidator
) : IMembershipTypeService
{
    /// <inheritdoc />
    public async Task<PagedResult<MembershipTypeResponse>> ListAsync(
        ListMembershipTypesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        var types = db.MembershipTypes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            types = types.Where(t => t.Name.ToLower().Contains(term));
        }

        if (query.IsActive is { } isActive)
        {
            types = types.Where(t => t.IsActive == isActive);
        }

        // Shortest plan first (daily, weekly, monthly), which is how a price list reads.
        return await Project(types.OrderBy(t => t.DurationDays).ThenBy(t => t.Name).ThenBy(t => t.Id))
            .ToPagedResultAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MembershipTypeOfferItem>> GetOfferAsync(CancellationToken cancellationToken = default) =>
        await db
            .MembershipTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DurationDays)
            .ThenBy(t => t.Price)
            .ThenBy(t => t.Name)
            .Select(t => new MembershipTypeOfferItem(t.Id, t.Name, t.DurationDays, t.Price))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<MembershipTypeResponse> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await Project(db.MembershipTypes.AsNoTracking().Where(t => t.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("MembershipType", id);

    /// <inheritdoc />
    public async Task<MembershipTypeResponse> CreateAsync(
        CreateMembershipTypeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name, exceptTypeId: null, cancellationToken);

        var type = new MembershipType(request.Name, request.DurationDays, request.Price);
        db.MembershipTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);

        // Brand new: nobody has bought it yet.
        return new MembershipTypeResponse(type.Id, type.Name, type.DurationDays, type.Price, type.IsActive, 0);
    }

    /// <inheritdoc />
    public async Task<MembershipTypeResponse> UpdateAsync(
        int id,
        UpdateMembershipTypeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var type = await FindAsync(id, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name, exceptTypeId: id, cancellationToken);

        // Safe for existing memberships: each one copied Price and DurationDays when it was bought
        // (Membership constructor), so this UPDATE touches only the membership_types row.
        type.Update(request.Name, request.DurationDays, request.Price);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var type = await FindAsync(id, cancellationToken);
        type.Deactivate(); // Idempotent. Member.PurchaseMembership refuses inactive types (Sprint 4).
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var type = await FindAsync(id, cancellationToken);
        type.Reactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var type = await FindAsync(id, cancellationToken);
        db.MembershipTypes.Remove(type);

        // memberships.membership_type_id is ON DELETE RESTRICT: a type that was ever bought stays
        // (history, reports). PostgreSQL refuses with 23001 → 409 "conflict.in_use", the same
        // pattern as rooms, workspaces and employees.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Entity → response as an expression, so EF builds ONE SQL query; the membership count is a
    /// correlated sub-select on memberships (MembershipType has no collection navigation).
    /// </summary>
    private IQueryable<MembershipTypeResponse> Project(IQueryable<MembershipType> types) =>
        types.Select(t => new MembershipTypeResponse(
            t.Id,
            t.Name,
            t.DurationDays,
            t.Price,
            t.IsActive,
            db.Memberships.Count(m => m.MembershipTypeId == t.Id)
        ));

    /// <summary>Loads a tracked membership type (so changes are saved) or throws 404.</summary>
    private async Task<MembershipType> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.MembershipTypes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException("MembershipType", id);

    /// <summary>Names are unique without regard to case; keeping its own name is fine.</summary>
    private async Task EnsureNameIsFreeAsync(string name, int? exceptTypeId, CancellationToken cancellationToken)
    {
        // Same comparison as the index ux_membership_types_name_lower: lower(trimmed name).
        var normalized = name.Trim().ToLowerInvariant();

        var taken = await db.MembershipTypes.AnyAsync(
            t => t.Name.ToLower() == normalized && t.Id != exceptTypeId,
            cancellationToken
        );

        if (taken)
        {
            throw new ConflictException(
                Localized.Text(
                    "A membership type with this name already exists.",
                    "Tip članarine s ovim nazivom već postoji."
                ),
                code: MembershipTypeErrorCodes.NameTaken
            );
        }
    }
}
