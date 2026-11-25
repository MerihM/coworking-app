using System.Globalization;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Auth.Validators;
using FluentValidation.TestHelper;

namespace Coworking.UnitTests.Auth;

/// <summary>
/// Input rules for registration (FR01) and the password policy. FluentValidation's TestHelper
/// (TestValidate + ShouldHaveValidationErrorFor) checks which FIELD failed, independent of the
/// message language.
/// </summary>
public sealed class RegisterMemberRequestValidatorTests
{
    private static readonly RegisterMemberRequest Valid =
        new("Amra", "Hodžić", "amra@example.com", "+387 61 123 456", "Coworking123");

    private readonly RegisterMemberRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        _validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("short1")] // too short
    [InlineData("abcdefgh")] // no digit
    [InlineData("12345678")] // no letter
    [InlineData("")]
    public void Password_BreakingThePolicy_IsRejected(string password)
    {
        _validator.TestValidate(Valid with { Password = password }).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("abcdefg1")] // exactly 8
    [InlineData("šifra2024")] // Bosnian letters count as letters
    public void Password_FollowingThePolicy_IsAccepted(string password)
    {
        _validator.TestValidate(Valid with { Password = password }).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_LongerThan128_IsRejected()
    {
        var password = new string('a', 128) + "1";

        _validator.TestValidate(Valid with { Password = password }).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void EmptyPassword_GetsOnlyOneMessage()
    {
        // RuleLevelCascadeMode.Stop: no "too short", "needs a letter", "needs a digit" pile-up.
        var result = _validator.TestValidate(Valid with { Password = "" });

        Assert.Single(result.Errors, e => e.PropertyName == nameof(RegisterMemberRequest.Password));
    }

    [Theory]
    [InlineData("+387 61 123 456")]
    [InlineData("061/123-456")]
    [InlineData("(033) 123 456")]
    public void Phone_CommonFormats_AreAccepted(string phone)
    {
        _validator.TestValidate(Valid with { Phone = phone }).ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")] // too short
    [InlineData("call me")]
    [InlineData("+387 61 123 456 ext. 2")]
    public void Phone_InvalidValues_AreRejected(string phone)
    {
        _validator.TestValidate(Valid with { Phone = phone }).ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Email_Invalid_IsRejected(string email)
    {
        _validator.TestValidate(Valid with { Email = email }).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Names_LongerThanTheColumn_AreRejected()
    {
        var result = _validator.TestValidate(
            Valid with { FirstName = new string('a', 101), LastName = new string('b', 101) }
        );

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Messages_FollowTheCurrentUiCulture()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("bs");
            var bosnian = _validator.TestValidate(Valid with { Password = "abcdefgh" });

            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var english = _validator.TestValidate(Valid with { Password = "abcdefgh" });

            Assert.Equal("Lozinka mora sadržavati najmanje jednu cifru.", bosnian.Errors.Single().ErrorMessage);
            Assert.Equal("Password must contain at least one digit.", english.Errors.Single().ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
