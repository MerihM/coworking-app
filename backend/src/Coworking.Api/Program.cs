// =====================================================================
// Coworking.Api – application entry point.
//
// Builds and starts the ASP.NET Core web server in two phases:
//   1) Services: register everything the app needs in the
//      dependency-injection (DI) container (builder.Services...).
//   2) Pipeline: define how each HTTP request flows through
//      middleware and reaches an endpoint (app.Use.../app.Map...).
// =====================================================================

using System.Diagnostics;
using Coworking.Api.ErrorHandling;
using Coworking.Application;
using Coworking.Infrastructure;
using Coworking.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// ---------------------------------------------------------------------
// Startup logger: console only, used ONLY until the real logger exists
// and for a crash during startup (e.g. invalid configuration).
// Note: we deliberately don't use Serilog's CreateBootstrapLogger(). It can only be
// "frozen" once per process, and integration tests start several API instances
// in one process, which would fail with "The logger is already frozen".
// ---------------------------------------------------------------------
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    Log.Information("Starting Coworking API");

    var builder = WebApplication.CreateBuilder(args);

    // -----------------------------------------------------------------
    // 1) SERVICES
    // -----------------------------------------------------------------

    // Serilog replaces the default logging providers. Sinks, levels and the file path
    // are read from the "Serilog" section of appsettings.json.
    // preserveStaticLogger: true = each API instance gets its own logger (disposed with the app)
    // instead of overwriting the global Log.Logger, which keeps parallel tests isolated.
    builder.Services.AddSerilog(
        (services, loggerConfiguration) =>
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services),
        preserveStaticLogger: true
    );

    // ProblemDetails (RFC 7807) for ALL error responses: exceptions, 404 for unknown routes,
    // and MVC's automatic 400 for invalid request bodies.
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            // Which URL failed, e.g. "/api/reservations"
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;

            // Correlation id: the same value appears in the log entry for this request,
            // so an error reported by the frontend can be found in the logs.
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
            );
        };
    });

    // Our handler that maps AppException subtypes to HTTP status codes.
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Application layer: FluentValidation validators (and services in later sprints).
    builder.Services.AddApplication();

    // Infrastructure layer: EF Core DbContext for PostgreSQL.
    builder.Services.AddInfrastructure();

    // MVC controllers. Feature controllers (Reservations, Memberships, ...) arrive from Sprint 2 on.
    builder.Services.AddControllers();

    // Built-in OpenAPI document generator (no Swashbuckle needed in .NET 9+).
    // Because GenerateDocumentationFile=true, /// comments on controllers
    // are copied into the generated document automatically.
    builder.Services.AddOpenApi(options =>
    {
        // A document transformer edits the generated document before it is served.
        options.AddDocumentTransformer(
            (document, _, _) =>
            {
                document.Info.Title = "Coworking API";
                document.Info.Version = "v1";
                document.Info.Description =
                    "REST API of the coworking center information system: members, memberships, "
                    + "payments, rooms, workspaces, reservations and reports.";
                return Task.CompletedTask;
            }
        );
    });

    // Health checks: /health returns "Healthy" only when the API is running AND PostgreSQL
    // accepts connections (EF Core's CanConnectAsync). Otherwise "Unhealthy" with HTTP 503.
    builder.Services.AddHealthChecks().AddDbContextCheck<CoworkingDbContext>("database");

    var app = builder.Build();

    // -----------------------------------------------------------------
    // 2) HTTP PIPELINE  (order matters: requests pass through top to bottom,
    //    responses come back bottom to top)
    // -----------------------------------------------------------------

    // FIRST, so it measures the whole request and sees the final status code,
    // including 500 responses written by the exception handler below.
    // Logs method, path (without query string), status and duration; never bodies or headers.
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            // Health checks are polled often (Docker, frontend): hide them from normal logs.
            : httpContext.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    });

    // Catches exceptions from everything below and passes them to GlobalExceptionHandler.
    app.UseExceptionHandler();

    // Turns empty error responses (e.g. 404 for an unknown URL) into ProblemDetails JSON.
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        // OpenAPI JSON document: /openapi/v1.json
        app.MapOpenApi();

        // Interactive API documentation UI: /scalar
        // Only in Development, so production does not expose internal API details.
        app.MapScalarApiReference(options => options.WithTitle("Coworking API"));
    }

    // NOTE: HTTPS is terminated by Nginx in front of the API (Deployment diagram, chapter 6.2),
    // so HTTPS redirection is configured together with the Docker/Nginx setup in a later sprint.
    // In local development the API runs on plain http://localhost:5080.

    // Returns 200 "Healthy" when all registered health checks pass.
    app.MapHealthChecks("/health");

    // Routes requests to controller actions ([Route], [HttpGet], ...).
    app.MapControllers();

    app.Run();
}
// HostAbortedException is thrown on purpose by tools such as "dotnet ef" (CW-7):
// they start Program only to read the configuration and then stop it. That is not a crash.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Coworking API terminated unexpectedly");
}
finally
{
    // Writes any buffered log events before the process exits.
    Log.CloseAndFlush();
}

/// <summary>
/// The application entry point class (generated by the compiler from the top-level statements above).
/// Declared <c>public partial</c> so that integration tests can start the API with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program { }
