using System.Text.Json;
using Coworking.Application.Common.Exceptions;
using Coworking.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coworking.Api.ErrorHandling;

/// <summary>
/// Catches every exception that escapes a controller or service and turns it into a
/// ProblemDetails (RFC 7807) JSON response with the correct HTTP status code.
/// Thanks to this class, controllers and services never need try/catch for error responses:
/// they simply throw an <see cref="AppException"/> subtype (or entities throw a <see cref="DomainException"/>).
/// </summary>
/// <remarks>
/// Registered with <c>AddExceptionHandler&lt;GlobalExceptionHandler&gt;()</c> and activated by
/// <c>app.UseExceptionHandler()</c> in Program.cs.
/// </remarks>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger
) : IExceptionHandler
{
    /// <summary>Code for a unique-index violation that no service checked first (race condition).</summary>
    private const string DuplicateCode = "conflict.duplicate";

    /// <summary>
    /// Code for a delete that a foreign key blocked: other rows still reference the record (CW-12).
    /// The frontend suggests deactivating it instead.
    /// </summary>
    private const string InUseCode = "conflict.in_use";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var problem = CreateProblemDetails(exception);
        var statusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        LogException(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

        // IProblemDetailsService writes the JSON, fills "type" and "title" defaults
        // for the status code and applies CustomizeProblemDetails from Program.cs (traceId, instance).
        // Returning true = "handled", the exception stops here.
        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception,
            }
        );
    }

    /// <summary>Maps an exception type to the ProblemDetails body (see the table in CW-5).</summary>
    private ProblemDetails CreateProblemDetails(Exception exception)
    {
        ProblemDetails problem = exception switch
        {
            // 400: FluentValidation found invalid input. "errors" lists messages per field:
            // { "errors": { "email": ["'E-mail' must not be empty."] } }
            ValidationException validation => new HttpValidationProblemDetails(
                GroupErrors(validation)
            )
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },

            // 401 (CW-11): wrong credentials or an account that no longer works.
            UnauthorizedException e => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = e.Message,
            },

            ForbiddenException e => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden.",
                Detail = e.Message,
            },

            NotFoundException e => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = e.Message,
            },

            ConflictException e => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The request conflicts with the current state.",
                Detail = e.Message,
            },

            // 409 (CW-11): a UNIQUE index rejected the insert (SqlState 23505), e.g. two people
            // registered the same e-mail at the same moment and both passed the service's check.
            // The safety net is the database; this turns its error into a proper 409 instead of 500.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The request conflicts with the current state.",
                    Detail = "This value is already in use.",
                    Extensions = { ["code"] = DuplicateCode },
                },

            // 409 (CW-12): DELETE refused by a foreign key, e.g. an employee who recorded payments.
            // History must stay, so the row can't go; the client should deactivate it instead.
            // PostgreSQL uses two codes for this: 23001 (restrict_violation) when the foreign key is
            // ON DELETE RESTRICT – checked immediately, which is what our EF configurations use – and
            // 23503 (foreign_key_violation) for the default NO ACTION, checked at the end of the
            // statement. Both mean the same here. Rooms/workspaces (Sprint 3) reuse this.
            DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation,
                },
            } =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The request conflicts with the current state.",
                    Detail = "This record is still in use by other data and can't be deleted. Deactivate it instead.",
                    Extensions = { ["code"] = InUseCode },
                },

            BusinessRuleException e => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "A business rule was violated.",
                Detail = e.Message,
            },

            // 422: an entity refused the operation (Domain layer's equivalent of BusinessRuleException).
            DomainException e => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "A business rule was violated.",
                Detail = e.Message,
            },

            // 500: a bug. In Development we show the message to speed up debugging;
            // in Production the client gets NO internal details (NFR01).
            // The stack trace is never sent to the client, only written to the log.
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = environment.IsDevelopment() ? exception.Message : null,
            },
        };

        // Business rule code (BR01–BR05) becomes an extra JSON field: "ruleCode": "BR04".
        // Both Application and Domain exceptions can carry one.
        var ruleCode = exception switch
        {
            AppException e => e.RuleCode,
            DomainException e => e.RuleCode,
            _ => null,
        };

        if (ruleCode is not null)
        {
            problem.Extensions["ruleCode"] = ruleCode;
        }

        // CW-11: stable error code for the frontend to translate: "code": "auth.email_taken".
        if (exception is AppException { Code: { } code })
        {
            problem.Extensions["code"] = code;
        }

        return problem;
    }

    /// <summary>
    /// Groups FluentValidation failures by property name: { "email": ["...", "..."] }.
    /// Keys are camelCase (CW-11) so they match the JSON field names the client sent
    /// ("firstName", not "FirstName"), and the frontend can put each message under its field.
    /// </summary>
    private static Dictionary<string, string[]> GroupErrors(ValidationException exception) =>
        exception
            .Errors.GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray()
            );

    /// <summary>
    /// Expected errors (4xx) are normal business outcomes: Information level, no stack trace.
    /// Unexpected errors (5xx) are bugs: Error level WITH the full exception.
    /// Since .NET 10 the exception handler middleware no longer logs exceptions that an
    /// IExceptionHandler has handled, so logging here is the only place they are recorded.
    /// </summary>
    private void LogException(HttpContext httpContext, Exception exception, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {RequestMethod} {RequestPath}",
                httpContext.Request.Method,
                httpContext.Request.Path
            );
        }
        else
        {
            logger.LogInformation(
                "{ExceptionType} while processing {RequestMethod} {RequestPath}: {ErrorMessage}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message
            );
        }
    }
}
