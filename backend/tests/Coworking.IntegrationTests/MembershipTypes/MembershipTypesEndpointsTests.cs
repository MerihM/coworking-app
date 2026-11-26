using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.MembershipTypes;
using Coworking.Application.MembershipTypes.Dtos;
using Coworking.Domain.Entities;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.MembershipTypes;

/// <summary>
/// /api/membership-types end-to-end (FR10, UC12): CRUD, price and duration rules, case-insensitive
/// unique names (service check AND database index), the public offer, FK-protected delete, the
/// "price change doesn't touch bought memberships" rule, and the NFR01 access matrix.
/// </summary>
public class MembershipTypesEndpointsTests(PostgresApiFactory factory)
    : IClassFixture<PostgresApiFactory>
{
    // ------------------------------------------------------------------ access control (NFR01)

    /// <summary>Management: only the Manager. Offer: everybody, including anonymous visitors.</summary>
    public static TheoryData<string, string?, HttpStatusCode> AccessMatrix()
    {
        var data = new TheoryData<string, string?, HttpStatusCode>();
        string?[] callers =
        [
            null,
            Roles.Member,
            Roles.Receptionist,
            Roles.Administrator,
            Roles.Manager,
        ];
        foreach (var caller in callers)
        {
            data.Add(
                "/api/membership-types",
                caller,
                caller is null ? HttpStatusCode.Unauthorized
                    : caller == Roles.Manager ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden
            );
            data.Add("/api/membership-types/offer", caller, HttpStatusCode.OK);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AccessMatrix))]
    public async Task NFR01_EachRoleReachesOnlyItsOwnFunctions(
        string url,
        string? role,
        HttpStatusCode expected
    )
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

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_Returns201_WithLocation_AndTrimmedName()
    {
        var manager = await factory.CreateManagerClientAsync();
        var name = $"Weekly {UniqueTag()}";

        var response = await manager.PostAsJsonAsync(
            "/api/membership-types",
            new CreateMembershipTypeRequest($"  {name} ", 7, 60.5m)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<MembershipTypeResponse>();
        Assert.EndsWith(
            $"/api/membership-types/{created!.Id}",
            response.Headers.Location?.ToString(),
            StringComparison.Ordinal
        );
        Assert.Equal(name, created.Name);
        Assert.Equal(7, created.DurationDays);
        Assert.Equal(60.50m, created.Price);
        Assert.True(created.IsActive);
        Assert.Equal(0, created.MembershipCount);
    }

    [Fact]
    public async Task Create_PriceIsReturnedAsJsonNumber()
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PostAsync(
            "/api/membership-types",
            JsonContent.Create(
                new
                {
                    name = UniqueTag(),
                    durationDays = 1,
                    price = 15,
                }
            )
        );

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Number, body.GetProperty("price").ValueKind);
        Assert.Equal(15m, body.GetProperty("price").GetDecimal());
    }

    [Theory]
    [InlineData(15.123)] // 3 decimals – money is never rounded silently
    [InlineData(0)] // free plans aren't part of the business
    [InlineData(-5)]
    [InlineData(10000.01)]
    public async Task Create_InvalidPrice_Returns400(double price)
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PostAsJsonAsync(
            "/api/membership-types",
            new CreateMembershipTypeRequest(UniqueTag(), 30, (decimal)price)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(
            "errors"
        );
        Assert.True(errors.TryGetProperty("price", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task Create_InvalidDuration_Returns400(int days)
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PostAsJsonAsync(
            "/api/membership-types",
            new CreateMembershipTypeRequest(UniqueTag(), days, 10m)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(
            "errors"
        );
        Assert.True(errors.TryGetProperty("durationDays", out _));
    }

    [Fact]
    public async Task Create_PriceAsString_Returns400()
    {
        var manager = await factory.CreateManagerClientAsync();

        // JSON contract (CW-13): "15" is a string, not a number.
        var response = await manager.PostAsync(
            "/api/membership-types",
            JsonContent.Create(
                new
                {
                    name = UniqueTag(),
                    durationDays = 1,
                    price = "15",
                }
            )
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameNameInOtherLetterCase_Returns409NameTaken()
    {
        var manager = await factory.CreateManagerClientAsync();
        var name = $"Monthly {UniqueTag()}";
        await factory.SeedMembershipTypeAsync(name);

        var response = await manager.PostAsJsonAsync(
            "/api/membership-types",
            new CreateMembershipTypeRequest(name.ToUpperInvariant(), 30, 99m)
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MembershipTypeErrorCodes.NameTaken, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ list, get, offer

    [Fact]
    public async Task List_SearchAndStatus_SortedByDuration()
    {
        var manager = await factory.CreateManagerClientAsync();
        var tag = UniqueTag();
        await factory.SeedMembershipTypeAsync($"{tag} Monthly", 30);
        await factory.SeedMembershipTypeAsync($"{tag} Daily", 1);
        await factory.SeedMembershipTypeAsync($"{tag} Old", 7, isActive: false);

        var active = await manager.GetFromJsonAsync<PagedResult<MembershipTypeResponse>>(
            $"/api/membership-types?search={tag.ToUpperInvariant()}&isActive=true"
        );
        var inactive = await manager.GetFromJsonAsync<PagedResult<MembershipTypeResponse>>(
            $"/api/membership-types?search={tag}&isActive=false"
        );

        Assert.Equal(new[] { $"{tag} Daily", $"{tag} Monthly" }, active!.Items.Select(t => t.Name));
        Assert.Equal($"{tag} Old", Assert.Single(inactive!.Items).Name);
    }

    [Fact]
    public async Task Get_CountsBoughtMemberships()
    {
        var manager = await factory.CreateManagerClientAsync();
        var type = await factory.SeedMembershipTypeAsync(UniqueTag());
        await factory.SeedMembershipAsync(type.Id);
        await factory.SeedMembershipAsync(type.Id);

        var result = await manager.GetFromJsonAsync<MembershipTypeResponse>(
            $"/api/membership-types/{type.Id}"
        );

        Assert.Equal(2, result!.MembershipCount);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.GetAsync("/api/membership-types/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Offer_WithoutToken_ShowsOnlyActiveTypes_ShortestFirst()
    {
        var tag = UniqueTag();
        var monthly = await factory.SeedMembershipTypeAsync($"{tag} Monthly", 30, 180m);
        var daily = await factory.SeedMembershipTypeAsync($"{tag} Daily", 1, 15m);
        var hidden = await factory.SeedMembershipTypeAsync(
            $"{tag} Hidden",
            7,
            60m,
            isActive: false
        );
        using var anonymous = factory.CreateHttpsClient();

        var offer = (
            await anonymous.GetFromJsonAsync<List<MembershipTypeOfferItem>>(
                "/api/membership-types/offer"
            )
        )!;

        // The shared test database contains other tests' types too: look only at ours.
        var ours = offer.Where(o => o.Name.StartsWith(tag, StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { daily.Id, monthly.Id }, ours.Select(o => o.Id));
        Assert.DoesNotContain(offer, o => o.Id == hidden.Id);
        Assert.Equal(15m, ours[0].Price);
    }

    [Fact]
    public async Task Offer_ResponseHasNoManagementFields()
    {
        await factory.SeedMembershipTypeAsync(UniqueTag());
        using var anonymous = factory.CreateHttpsClient();

        var offer = await anonymous.GetFromJsonAsync<JsonElement>("/api/membership-types/offer");

        // Visitors see the price list, not usage numbers or inactive flags.
        var first = offer.EnumerateArray().First();
        Assert.False(first.TryGetProperty("membershipCount", out _));
        Assert.False(first.TryGetProperty("isActive", out _));
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_ChangesAllFields()
    {
        var manager = await factory.CreateManagerClientAsync();
        var type = await factory.SeedMembershipTypeAsync(UniqueTag());
        var newName = $"Student {UniqueTag()}";

        var response = await manager.PutAsJsonAsync(
            $"/api/membership-types/{type.Id}",
            new UpdateMembershipTypeRequest(newName, 31, 99.99m)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<MembershipTypeResponse>();
        Assert.Equal(newName, updated!.Name);
        Assert.Equal(31, updated.DurationDays);
        Assert.Equal(99.99m, updated.Price);
    }

    [Fact]
    public async Task Update_PriceAndDuration_DoNotChangeAlreadyBoughtMemberships()
    {
        // Class diagram: Clanarina keeps "cijena u trenutku kupovine" – the price at purchase time.
        var manager = await factory.CreateManagerClientAsync();
        var type = await factory.SeedMembershipTypeAsync(UniqueTag(), 30, 120m);
        var membershipId = await factory.SeedMembershipAsync(type.Id);

        var response = await manager.PutAsJsonAsync(
            $"/api/membership-types/{type.Id}",
            new UpdateMembershipTypeRequest(type.Name, 60, 200m)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var membership = await db.Memberships.AsNoTracking().SingleAsync(m => m.Id == membershipId);
        Assert.Equal(120m, membership.Price);
        Assert.Equal(30, membership.DurationDays);
    }

    [Fact]
    public async Task Update_NameOfAnotherType_Returns409NameTaken()
    {
        var manager = await factory.CreateManagerClientAsync();
        var taken = await factory.SeedMembershipTypeAsync($"Taken {UniqueTag()}");
        var type = await factory.SeedMembershipTypeAsync(UniqueTag());

        var response = await manager.PutAsJsonAsync(
            $"/api/membership-types/{type.Id}",
            new UpdateMembershipTypeRequest(taken.Name.ToLowerInvariant(), 30, 120m)
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MembershipTypeErrorCodes.NameTaken, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ deactivate, activate

    [Fact]
    public async Task DeactivateThenActivate_RemovesFromAndReturnsToTheOffer()
    {
        var manager = await factory.CreateManagerClientAsync();
        var type = await factory.SeedMembershipTypeAsync(UniqueTag());
        using var anonymous = factory.CreateHttpsClient();

        var deactivate = await manager.PostAsync(
            $"/api/membership-types/{type.Id}/deactivate",
            null
        );
        var offerWhileInactive = await anonymous.GetFromJsonAsync<List<MembershipTypeOfferItem>>(
            "/api/membership-types/offer"
        );
        var activate = await manager.PostAsync($"/api/membership-types/{type.Id}/activate", null);
        var offerAfter = await anonymous.GetFromJsonAsync<List<MembershipTypeOfferItem>>(
            "/api/membership-types/offer"
        );

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.DoesNotContain(offerWhileInactive!, o => o.Id == type.Id);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Contains(offerAfter!, o => o.Id == type.Id);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_NeverBought_Returns204_ThenGetReturns404()
    {
        var manager = await factory.CreateManagerClientAsync();
        var type = await factory.SeedMembershipTypeAsync(UniqueTag());

        var delete = await manager.DeleteAsync($"/api/membership-types/{type.Id}");
        var get = await manager.GetAsync($"/api/membership-types/{type.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_Bought_Returns409InUse()
    {
        var manager = await factory.CreateManagerClientAsync();
        var type = await factory.SeedMembershipTypeAsync(UniqueTag());
        await factory.SeedMembershipAsync(type.Id);

        var response = await manager.DeleteAsync($"/api/membership-types/{type.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict.in_use", await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ database backstop

    [Fact]
    public async Task Database_RejectsSameNameInOtherCase_EvenWithoutTheServiceCheck()
    {
        var name = $"Race {UniqueTag()}";
        await factory.SeedMembershipTypeAsync(name);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        db.MembershipTypes.Add(new MembershipType(name.ToUpperInvariant(), 30, 120m));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_membership_types_name_lower", postgres.ConstraintName);
    }
}
