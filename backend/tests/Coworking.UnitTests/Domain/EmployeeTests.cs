using static Coworking.UnitTests.Domain.TestData;

namespace Coworking.UnitTests.Domain;

/// <summary>Employee rules added in CW-12.</summary>
public class EmployeeTests
{
    [Fact]
    public void ChangeEmail_TrimsAndLowerCases()
    {
        var employee = NewReceptionist();

        employee.ChangeEmail("  Emir.Kovac@Coworking.BA ");

        Assert.Equal("emir.kovac@coworking.ba", employee.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeEmail_Empty_Throws(string email)
    {
        Assert.Throws<ArgumentException>(() => NewReceptionist().ChangeEmail(email));
    }
}
