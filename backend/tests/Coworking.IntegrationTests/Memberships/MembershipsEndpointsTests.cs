using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Members;
using Coworking.Application.Memberships;
using Coworking.Application.Memberships.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.Memberships;

/// <summary>
/// Buying memberships (FR03, UC03) and recording payments (FR07, UC09) end-to-end: TC07, the BR05
/// renewal start, the payment and purchase guards, ownership of "mine", and the NFR01 matrix.
/// </summary>
public class MembershipsEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    /// <summary>Enums as names in BOTH directions, like the API (allowIntegerValues: false there).</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static DateOnly Today => CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow);

    // ------------------------------------------------------------------ access control (NFR01)

    public static TheoryData<string, string?, HttpStatusCode> AccessMatrix()
    {
        var data = new TheoryData<string, string?, HttpStatusCode>();
        string?[] callers = [null, Roles.Member, Roles.Receptionist, Roles.Manager, Roles.Administrator];
        foreach (var caller in callers)
        {
            data.Add(
                "/api/memberships",
                caller,
                caller is null ? HttpStatusCode.Unauthorized
                    : caller is Roles.Receptionist or Roles.Manager ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden
            );
            data.Add(
                "/api/memberships/mine",
                caller,
                caller is null ? HttpStatusCode.Unauthorized
                    : caller == Roles.Member ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden
            );
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AccessMatrix))]
    public async Task NFR01_EachRoleReachesOnlyItsOwnFunctions(string url, string? role, HttpStatusCode expected)
    {
        HttpResponseMessage response;
        if (role is null)
        {
            using var anonymous = factory.CreateHttpsClient();
            response = await anonymous.GetAsync(url);
        }
        else
        {
            using var caller = await factory.CreateClientAsAsync(role);
            response = await caller.Client.GetAsync(url);
        }

        Assert.Equal(expected, response.StatusCode);
    }

    // ------------------------------------------------------------------ TC07 + BR05

    [Fact]
    public async Task TC07_MemberBuysMonthly_ReceptionRecordsFullPayment_MembershipIsActiveForTheDuration()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Monthly {UniqueTag()}", 30, 180m);

        // UC03 step 4: Unpaid, with the amount to pay and no period yet.
        var bought = await BuyAsync(member.Client, type.Id);
        Assert.Equal(MembershipStatus.Unpaid, bought.Status);
        Assert.Equal(180m, bought.Price);
        Assert.Null(bought.StartDate);
        Assert.Null(bought.Payment);

        // UC09: payment in full → Active from today, EndDate = start + duration (exclusive).
        var paid = await PayAsync(reception.Client, bought.Id, 180m, PaymentMethod.Card);
        Assert.Equal(MembershipStatus.Active, paid.Status);
        Assert.Equal(Today, paid.StartDate);
        Assert.Equal(Today.AddDays(30), paid.EndDate);
        Assert.Equal(Today.AddDays(29), paid.LastDay);
        Assert.Equal(PaymentMethod.Card, paid.Payment!.Method);
        Assert.Equal(180m, paid.Payment!.Amount);

        // "... and the member can reserve": BR02 is satisfied today (reservation API: Sprint 5).
        Assert.True(await HasActiveMembershipTodayAsync(member.User.Id));

        // The member sees the same in their own list.
        var mine = await member.Client.GetFromJsonAsync<List<MembershipResponse>>("/api/memberships/mine", Json);
        Assert.Equal(MembershipStatus.Active, Assert.Single(mine!).Status);
    }

    [Fact]
    public async Task BR05_PayingARenewal_StartsWhenTheCurrentMembershipEnds()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Weekly {UniqueTag()}", 7, 60m);

        var first = await PayAsync(reception.Client, (await BuyAsync(member.Client, type.Id)).Id, 60m, PaymentMethod.Cash);
        var renewal = await PayAsync(reception.Client, (await BuyAsync(member.Client, type.Id)).Id, 60m, PaymentMethod.Cash);

        Assert.Equal(first.EndDate, renewal.StartDate);
        Assert.Equal(Today.AddDays(14), renewal.EndDate);
    }

    [Fact]
    public async Task RecordPayment_AmountDiffersFromPrice_Returns422Br05_AndStaysUnpaid()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Monthly {UniqueTag()}", 30, 180m);
        var bought = await BuyAsync(member.Client, type.Id);

        var response = await reception.Client.PostAsJsonAsync(
            $"/api/memberships/{bought.Id}/payment",
            new RecordPaymentRequest(100m, PaymentMethod.Cash),
            Json
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BR05", problem.GetProperty("ruleCode").GetString());
        Assert.False(await HasActiveMembershipTodayAsync(member.User.Id));
    }

    [Fact]
    public async Task RecordPayment_Twice_Returns409AlreadyPaid()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        var bought = await BuyAsync(member.Client, type.Id);
        await PayAsync(reception.Client, bought.Id, 15m, PaymentMethod.Cash);

        var response = await reception.Client.PostAsJsonAsync(
            $"/api/memberships/{bought.Id}/payment",
            new RecordPaymentRequest(15m, PaymentMethod.Cash),
            Json
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MembershipErrorCodes.AlreadyPaid, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task RecordPayment_MethodAsNumber_Returns400()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);

        var response = await reception.Client.PostAsJsonAsync(
            "/api/memberships/1/payment",
            new { amount = 15, method = 0 }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ buying

    [Fact]
    public async Task Buy_InactiveType_Returns422TypeInactive()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        var type = await factory.SeedMembershipTypeAsync($"Old {UniqueTag()}", isActive: false);

        var response = await member.Client.PostAsJsonAsync("/api/memberships/mine", new BuyMembershipRequest(type.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(MembershipErrorCodes.TypeInactive, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Buy_WhileAnUnpaidOneExists_Returns409UnpaidExists()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        await BuyAsync(member.Client, type.Id);

        var response = await member.Client.PostAsJsonAsync("/api/memberships/mine", new BuyMembershipRequest(type.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MembershipErrorCodes.UnpaidExists, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Sell_ToADeactivatedMember_Returns422MemberInactive()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        await reception.Client.PostAsync($"/api/members/{member.User.Id}/deactivate", null);

        var response = await reception.Client.PostAsJsonAsync(
            $"/api/members/{member.User.Id}/memberships",
            new BuyMembershipRequest(type.Id)
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(MemberErrorCodes.Inactive, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ delete + ownership

    [Fact]
    public async Task DeleteMine_OwnUnpaid_Returns204_SomeoneElses_Returns404()
    {
        using var owner = await factory.CreateClientAsAsync(Roles.Member);
        using var other = await factory.CreateClientAsAsync(Roles.Member);
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        var bought = await BuyAsync(owner.Client, type.Id);

        var foreign = await other.Client.DeleteAsync($"/api/memberships/mine/{bought.Id}");
        var own = await owner.Client.DeleteAsync($"/api/memberships/mine/{bought.Id}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
    }

    [Fact]
    public async Task Delete_Paid_Returns422AlreadyPaid()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        var paid = await PayAsync(reception.Client, (await BuyAsync(member.Client, type.Id)).Id, 15m, PaymentMethod.Cash);

        var response = await reception.Client.DeleteAsync($"/api/memberships/{paid.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(MembershipErrorCodes.AlreadyPaid, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ reception list

    [Fact]
    public async Task List_FilterByMemberAndStatus()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        var paid = await PayAsync(reception.Client, (await BuyAsync(member.Client, type.Id)).Id, 15m, PaymentMethod.Cash);
        var unpaid = await BuyAsync(member.Client, type.Id);

        var unpaidPage = await reception.Client.GetFromJsonAsync<PagedResult<MembershipResponse>>(
            $"/api/memberships?memberId={member.User.Id}&status=Unpaid",
            Json
        );
        var activePage = await reception.Client.GetFromJsonAsync<PagedResult<MembershipResponse>>(
            $"/api/memberships?memberId={member.User.Id}&status=Active",
            Json
        );

        Assert.Equal(unpaid.Id, Assert.Single(unpaidPage!.Items).Id);
        Assert.Equal(paid.Id, Assert.Single(activePage!.Items).Id);
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<MembershipResponse> BuyAsync(HttpClient member, int typeId)
    {
        var response = await member.PostAsJsonAsync("/api/memberships/mine", new BuyMembershipRequest(typeId), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MembershipResponse>(Json))!;
    }

    private static async Task<MembershipResponse> PayAsync(
        HttpClient reception,
        int membershipId,
        decimal amount,
        PaymentMethod method
    )
    {
        var response = await reception.PostAsJsonAsync(
            $"/api/memberships/{membershipId}/payment",
            new RecordPaymentRequest(amount, method),
            Json
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MembershipResponse>(Json))!;
    }

    /// <summary>Asks the domain itself (BR02, Member.HasActiveMembershipOn) – what Sprint 5's reservations will call.</summary>
    private async Task<bool> HasActiveMembershipTodayAsync(int memberId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var member = await db.Members.Include(m => m.Memberships).SingleAsync(m => m.Id == memberId);
        return member.HasActiveMembershipOn(Today);
    }
}
