using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Common.Pagination;

/// <summary>
/// Base validator for list queries: checks page and pageSize. A concrete list validator inherits
/// from it and adds rules for its own filters. Being abstract, it is not registered by
/// <c>AddValidatorsFromAssembly</c> itself – only its concrete subclasses are.
/// </summary>
/// <typeparam name="T">The list query type.</typeparam>
public abstract class PageQueryValidator<T> : AbstractValidator<T>
    where T : PageQuery
{
    /// <summary>Upper bound for <see cref="PageQuery.Page"/>, so (page − 1) × pageSize can't overflow.</summary>
    public const int MaxPage = 100_000;

    /// <summary>Defines the paging rules.</summary>
    protected PageQueryValidator()
    {
        RuleFor(x => x.Page)
            .InclusiveBetween(1, MaxPage)
            .WithName(_ => Localized.Text("Page", "Stranica"));

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PageQuery.MaxPageSize)
            .WithName(_ => Localized.Text("Page size", "Veličina stranice"));
    }
}
