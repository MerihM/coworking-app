using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Coworking.Api.OpenApi;

/// <summary>
/// Adds a "Bearer" security scheme to the OpenAPI document so Scalar shows an auth field where
/// you can paste an access token and call protected endpoints.
/// </summary>
/// <remarks>
/// .NET 10 uses Microsoft.OpenApi 2.x, where model types live directly in the
/// <c>Microsoft.OpenApi</c> namespace (no more <c>.Models</c>) and references are created with
/// <see cref="OpenApiSecuritySchemeReference"/>.
/// The requirement is added at document level (applies to all operations). Anonymous endpoints
/// simply ignore the header, so this is harmless for login/register.
/// </remarks>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Key of the scheme inside components.securitySchemes.</summary>
    private const string SchemeId = "Bearer";

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from POST /api/auth/login (valid 15 minutes).",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = [],
        });

        return Task.CompletedTask;
    }
}
