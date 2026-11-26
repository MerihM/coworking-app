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
using System.Text.Json.Serialization;
using Coworking.Api.Auth;
using Coworking.Api.ErrorHandling;
using Coworking.Api.Localization;
using Coworking.Api.OpenApi;
using Coworking.Api.RateLimiting;
using Coworking.Application;
using Coworking.Infrastructure;
using Coworking.Infrastructure.Persistence;
using Coworking.Infrastructure.Seeding;
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

    // Application layer: FluentValidation validators and use-case services (AuthService, ...).
    builder.Services.AddApplication();

    // Infrastructure layer: EF Core DbContext for PostgreSQL, password hashing, JWT and refresh tokens.
    builder.Services.AddInfrastructure();

    // MVC controllers.
    // SuppressImplicitRequiredAttribute...: by default MVC treats every non-nullable string in a
    // request body as [Required] and answers 400 with its own English message BEFORE our code
    // runs. We switch that off so FluentValidation is the single place for input rules, and its
    // messages can be translated (CW-11).
    builder
        .Services.AddControllers(options =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true
        )
        // Enums travel as their NAMES in JSON ("Manager"), not as numbers (1) – in both directions.
        // Readable for humans, and reordering an enum can't silently change what a number means (CW-12).
        // NumberHandling.Strict (CW-13): numbers must be real JSON numbers (5, not "5"). The web
        // default also accepts numbers written as strings, which made the OpenAPI document describe
        // every int as "integer | string" – and the generated TypeScript types as number | string.
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(StrictEnumConverter());
            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

    // The OpenAPI generator reads the "minimal API" JSON options, not MVC's, so the same settings
    // are registered there too. Then the generated TypeScript types say role: "Receptionist" | ...
    // and page: number.
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(StrictEnumConverter());
        options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });

    // allowIntegerValues: false – without it the converter WRITES names but still ACCEPTS numbers
    // ("role": 2 would silently mean Administrator). Names only, in both directions.
    static JsonStringEnumConverter StrictEnumConverter() =>
        new(namingPolicy: null, allowIntegerValues: false);

    // Built-in OpenAPI document generator (no Swashbuckle needed in .NET 9+).
    // Because GenerateDocumentationFile=true, /// comments on controllers
    // are copied into the generated document automatically.
    // The transformer adds the "Bearer" scheme so Scalar can send access tokens (CW-9).
    builder.Services.AddOpenApi(options =>
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>()
    );

    // Authentication (who is calling: JWT bearer tokens) and authorization (which role may do
    // what: named policies). Both live in Api/Auth – see ADR 0003.
    builder.Services.AddJwtAuthentication();
    builder.Services.AddAuthorizationPolicies();

    // Rate limits per IP: login/register 5 per minute (CW-11), public endpoints such as the
    // availability search 60 per minute (CW-21). Endpoints opt in with [EnableRateLimiting].
    builder.Services.AddAuthRateLimiting();

    // Health checks: /health returns "Healthy" only when the API is running AND PostgreSQL
    // accepts connections (EF Core's CanConnectAsync). Otherwise "Unhealthy" with HTTP 503.
    builder.Services.AddHealthChecks().AddDbContextCheck<CoworkingDbContext>("database");

    var app = builder.Build();

    // Creates the first administrator from Seed:Admin (user-secrets) if no active administrator
    // exists yet (CW-12). Does nothing – and doesn't touch the database – when Seed:Admin is not set.
    await app.Services.SeedAdministratorAsync();

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

    // Sets the request language (en/bs) from the Accept-Language header, BEFORE anything that
    // produces text (validation messages). See Localization/LocalizationSetup.cs (CW-11).
    app.UseApiLocalization();

    // Rejects too many login/register attempts (and public searches) early, before any password
    // hashing or database work (CW-11, CW-21).
    app.UseRateLimiter();

    // Reads the "Authorization: Bearer <token>" header and, if the token is valid, fills
    // HttpContext.User with its claims (CW-9). Does not reject anything by itself.
    app.UseAuthentication();

    // Checks the endpoint's rules ([Authorize], policies, RequireAuthorization below) against
    // HttpContext.User: 401 when not logged in, 403 when the role is not allowed (NFR01).
    app.UseAuthorization();

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

    // Returns 200 "Healthy" when all registered health checks pass. Public (no token needed),
    // because Docker and monitoring call it without logging in.
    app.MapHealthChecks("/health");

    // Routes requests to controller actions ([Route], [HttpGet], ...).
    // RequireAuthorization = secure by default: every controller action needs a valid token
    // unless it is marked [AllowAnonymous] (e.g. login and register). See ADR 0003.
    app.MapControllers().RequireAuthorization();

    await app.RunAsync();
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
