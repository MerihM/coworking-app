using Microsoft.AspNetCore.Localization;

namespace Coworking.Api.Localization;

/// <summary>
/// Request language: English (default) or Bosnian, chosen by the <c>Accept-Language</c> header
/// that the frontend sends with the language the user picked (UI decision at Sprint 2 start).
/// </summary>
/// <remarks>
/// The middleware sets <c>CultureInfo.CurrentCulture</c> and <c>CurrentUICulture</c> for the
/// request. FluentValidation and <c>Localized.Text</c> read CurrentUICulture to pick the message
/// language. "bs-BA" or "bs-Latn-BA" fall back to "bs"; anything else (de, hr, ...) gets English.
/// </remarks>
public static class LocalizationSetup
{
    /// <summary>Supported languages; the first is the default.</summary>
    public static readonly string[] SupportedCultures = ["en", "bs"];

    /// <summary>Adds the request localization middleware.</summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The same pipeline, for chaining.</returns>
    public static IApplicationBuilder UseApiLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(options =>
        {
            options
                .SetDefaultCulture(SupportedCultures[0])
                .AddSupportedCultures(SupportedCultures)
                .AddSupportedUICultures(SupportedCultures);

            // Only the header. The defaults would also accept ?culture=... and a culture cookie,
            // which this API doesn't need.
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];

            // Adds "Content-Language: bs" to responses, so clients (and tests) see which language was used.
            options.ApplyCurrentCultureToResponseHeaders = true;
        });
}
