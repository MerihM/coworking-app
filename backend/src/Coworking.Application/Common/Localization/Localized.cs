using System.Globalization;

namespace Coworking.Application.Common.Localization;

/// <summary>
/// Picks the English or Bosnian version of a text for the language of the current request.
/// </summary>
/// <remarks>
/// <para>
/// The API sets <see cref="CultureInfo.CurrentUICulture"/> from the request's
/// <c>Accept-Language</c> header (Program.cs, <c>UseRequestLocalization</c>). FluentValidation's
/// own messages ("'Email' must not be empty.") follow that culture automatically and ship with
/// Bosnian translations. This helper covers OUR custom messages and field names.
/// </para>
/// <para>
/// Only validation messages are translated on the server. All other errors carry a stable
/// <c>code</c> that the frontend translates, so most texts live in one place (the frontend).
/// .resx resource files would be the "big project" way; with two languages and a handful of
/// texts, keeping both versions side by side is easier to read and review.
/// </para>
/// </remarks>
public static class Localized
{
    /// <summary><c>true</c> when the current request asked for Bosnian (bs, bs-BA, bs-Latn-BA...).</summary>
    public static bool IsBosnian => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "bs";

    /// <summary>Returns the text in the current request's language.</summary>
    /// <param name="english">English text (default).</param>
    /// <param name="bosnian">Bosnian text.</param>
    /// <returns><paramref name="bosnian"/> for Bosnian requests, otherwise <paramref name="english"/>.</returns>
    public static string Text(string english, string bosnian) => IsBosnian ? bosnian : english;
}
