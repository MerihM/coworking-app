using Coworking.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.ErrorHandling;

/// <summary>
/// Catches every exception that escapes a controller or service and turns it into a
/// ProblemDetails (RFC 7807) JSON response with the correct HTTP status code.
/// Thanks to this class, controllers and services never need try/catch for error responses:
/// they simply throw an <see cref="AppException"/> subtype.
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
            // { "errors": { "Email": ["Email is required."] } }
            ValidationException validation => new HttpValidationProblemDetails(
                GroupErrors(validation)
            )
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
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

            BusinessRuleException e => new ProblemDetails
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

        // Business rule code (BR01–BR05) becomes an extra JSON field: "ruleCode": "BR04"
        if (exception is AppException { RuleCode: { } ruleCode })
        {
            problem.Extensions["ruleCode"] = ruleCode;
        }

        return problem;
    }

    /// <summary>Groups FluentValidation failures by property name: { "Email": ["...", "..."] }.</summary>
    private static Dictionary<string, string[]> GroupErrors(ValidationException exception) =>
        exception
            .Errors.GroupBy(failure => failure.PropertyName)
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
