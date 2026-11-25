using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using static Coworking.UnitTests.Domain.TestData;

namespace Coworking.UnitTests.Domain;

/// <summary>Refresh token rules: lifetime (half-open), rotation, revocation, one owner.</summary>
public class RefreshTokenTests
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private static byte[] NewHash(byte fill = 1) => Enumerable.Repeat(fill, RefreshToken.HashLength).ToArray();

    private static RefreshToken NewMemberToken() =>
        RefreshToken.ForMember(5, NewHash(), Guid.NewGuid(), Now, Week);

    [Fact]
    public void ForMember_SetsOwnerAndLifetime()
    {
        var token = NewMemberToken();

        Assert.Equal(5, token.MemberId);
        Assert.Null(token.EmployeeId);
        Assert.Equal(Now, token.CreatedAt);
        Assert.Equal(Now + Week, token.ExpiresAt);
        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public void ForEmployee_SetsOnlyEmployeeOwner()
    {
        var token = RefreshToken.ForEmployee(3, NewHash(), Guid.NewGuid(), Now, Week);

        Assert.Equal(3, token.EmployeeId);
        Assert.Null(token.MemberId);
    }

    [Fact]
    public void IsActive_OneSecondBeforeExpiry_True_AtExpiry_False()
    {
        // Half-open period [CreatedAt, ExpiresAt), like reservations and memberships (ADR 0002).
        var token = NewMemberToken();

        Assert.True(token.IsActive(token.ExpiresAt.AddSeconds(-1)));
        Assert.False(token.IsActive(token.ExpiresAt));
        Assert.True(token.IsExpired(token.ExpiresAt));
    }

    [Fact]
    public void Rotate_ReturnsSuccessorInSameFamilyAndRevokesOld()
    {
        var token = NewMemberToken();
        var later = Now.AddHours(1);

        var next = token.Rotate(NewHash(2), later, Week);

        Assert.Equal(token.FamilyId, next.FamilyId);
        Assert.Equal(token.MemberId, next.MemberId);
        Assert.Equal(later + Week, next.ExpiresAt);
        Assert.True(next.IsActive(later));

        Assert.Equal(later, token.RevokedAt);
        Assert.Same(next, token.ReplacedBy);
        Assert.True(token.WasRotated);
        Assert.False(token.IsActive(later));
    }

    [Fact]
    public void Rotate_RevokedToken_Throws()
    {
        var token = NewMemberToken();
        token.Revoke(Now);

        Assert.Throws<DomainException>(() => token.Rotate(NewHash(2), Now, Week));
    }

    [Fact]
    public void Rotate_ExpiredToken_Throws()
    {
        var token = NewMemberToken();

        Assert.Throws<DomainException>(() => token.Rotate(NewHash(2), token.ExpiresAt, Week));
    }

    [Fact]
    public void Revoke_Twice_KeepsFirstRevocationTime()
    {
        var token = NewMemberToken();

        token.Revoke(Now);
        token.Revoke(Now.AddHours(2));

        Assert.Equal(Now, token.RevokedAt);
        Assert.False(token.WasRotated); // revoked by logout, not by rotation
    }

    [Fact]
    public void Create_CopiesTheHash()
    {
        var hash = NewHash();
        var token = RefreshToken.ForMember(5, hash, Guid.NewGuid(), Now, Week);

        hash[0] = 99;

        Assert.Equal(1, token.TokenHash[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void Create_HashWithWrongLength_Throws(int length)
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.ForMember(5, new byte[length], Guid.NewGuid(), Now, Week)
        );
    }

    [Fact]
    public void Create_InvalidArguments_Throw()
    {
        Assert.ThrowsAny<ArgumentException>(() => RefreshToken.ForMember(0, NewHash(), Guid.NewGuid(), Now, Week));
        Assert.ThrowsAny<ArgumentException>(() => RefreshToken.ForEmployee(-1, NewHash(), Guid.NewGuid(), Now, Week));
        Assert.ThrowsAny<ArgumentException>(() => RefreshToken.ForMember(5, NewHash(), Guid.Empty, Now, Week));
        Assert.ThrowsAny<ArgumentException>(() => RefreshToken.ForMember(5, NewHash(), Guid.NewGuid(), Now, TimeSpan.Zero));
    }
}
