using Coworking.Application.MembershipTypes.Dtos;
using Coworking.Application.MembershipTypes.Validators;

namespace Coworking.UnitTests.MembershipTypes;

/// <summary>
/// Price and duration rules of a membership type (FR10), checked without HTTP or a database.
/// The integration tests cover the same rules end-to-end; these show the edge cases quickly.
/// </summary>
public class MembershipTypeRequestValidatorTests
{
    private readonly CreateMembershipTypeRequestValidator _validator = new();

    [Theory]
    [InlineData("0.01")]
    [InlineData("15")]
    [InlineData("15.5")]
    [InlineData("15.50")]
    [InlineData("15.500")] // trailing zeros are ignored: it is still 15.5
    [InlineData("10000")]
    public void Price_ValidValues_Pass(string price)
    {
        var result = _validator.Validate(Request(price: decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("15.505")]
    [InlineData("10000.01")]
    public void Price_InvalidValues_Fail(string price)
    {
        var result = _validator.Validate(Request(price: decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateMembershipTypeRequest.Price));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(0, false)]
    [InlineData(366, false)]
    public void Duration_MustBeOneDayToOneYear(int days, bool valid)
    {
        var result = _validator.Validate(Request(durationDays: days));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" A ")]
    public void Name_TooShortAfterTrimming_Fails(string name)
    {
        var result = _validator.Validate(Request(name: name));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateMembershipTypeRequest.Name));
    }

    private static CreateMembershipTypeRequest Request(string name = "Monthly", int durationDays = 30, decimal price = 120m) =>
        new(name, durationDays, price);
}
