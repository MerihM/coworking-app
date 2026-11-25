using Coworking.Application.Auth;
using Coworking.Application.Employees;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.Application;

/// <summary>
/// Registers all services of the Application layer in the DI container.
/// Program.cs calls <c>builder.Services.AddApplication()</c>, so the API project does not need
/// to know the individual classes. Infrastructure gets the same pattern in CW-7.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Adds Application-layer services: validators and use-case services.</summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Finds every AbstractValidator<T> in this assembly and registers it,
        // so new validators work without touching this file.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Use-case services. Scoped = one instance per HTTP request, like the DbContext they use.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmployeeService, EmployeeService>();

        return services;
    }
}
