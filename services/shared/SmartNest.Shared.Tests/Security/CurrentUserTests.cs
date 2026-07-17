using FluentAssertions;
using SmartNest.Shared.Security;
using System.Security.Claims;
using Xunit;

namespace SmartNest.Shared.Tests.Security;

public class CurrentUserTests
{
    [Fact]
    public void FromAuthorizationHeader_ParsesClaims_WhenTokenIsWellFormed()
    {
        var token = JwtTestTokenFactory.Create("user-oid-1", new[] { "SmartNest.Owner" }, "home-1");

        var user = CurrentUser.FromAuthorizationHeader($"Bearer {token}");

        user.UserId.Should().Be("user-oid-1");
        user.Roles.Should().ContainSingle().Which.Should().Be("SmartNest.Owner");
        user.HomeId.Should().Be("home-1");
    }

    [Fact]
    public void FromAuthorizationHeader_ParsesMultipleRoles()
    {
        var token = JwtTestTokenFactory.Create(
            "user-oid-1", new[] { "SmartNest.Owner", "SmartNest.Technician" }, "home-1");

        var user = CurrentUser.FromAuthorizationHeader($"Bearer {token}");

        user.Roles.Should().BeEquivalentTo(new[] { "SmartNest.Owner", "SmartNest.Technician" });
        user.HasRole("smartnest.owner").Should().BeTrue("role checks should be case-insensitive");
    }

    [Fact]
    public void FromAuthorizationHeader_AllowsMissingHomeIdClaim()
    {
        var token = JwtTestTokenFactory.Create("user-oid-1", new[] { "SmartNest.Owner" }, homeId: null);

        var user = CurrentUser.FromAuthorizationHeader($"Bearer {token}");

        user.HomeId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromAuthorizationHeader_Throws_WhenHeaderMissingOrEmpty(string? header)
    {
        var act = () => CurrentUser.FromAuthorizationHeader(header);

        act.Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void FromAuthorizationHeader_Throws_WhenTokenIsMalformed()
    {
        var act = () => CurrentUser.FromAuthorizationHeader("Bearer not-a-real-jwt");

        act.Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void FromAuthorizationHeader_Throws_WhenSubjectClaimMissing()
    {
        var token = JwtTestTokenFactory.Create(oid: null, roles: new[] { "SmartNest.Owner" }, homeId: "home-1");

        var act = () => CurrentUser.FromAuthorizationHeader($"Bearer {token}");

        act.Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void FromClaimsPrincipal_ParsesClaims_WhenPrincipalIsWellFormed()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("oid", "user-oid-1"),
            new Claim("roles", "SmartNest.Owner"),
            new Claim("roles", "SmartNest.Technician"),
            new Claim("homeId", "home-1"),
        });
        var principal = new ClaimsPrincipal(identity);

        var user = CurrentUser.FromClaimsPrincipal(principal);

        user.UserId.Should().Be("user-oid-1");
        user.Roles.Should().BeEquivalentTo(new[] { "SmartNest.Owner", "SmartNest.Technician" });
        user.HomeId.Should().Be("home-1");
    }

    [Fact]
    public void FromClaimsPrincipal_Throws_WhenSubjectClaimMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("roles", "SmartNest.Owner") }));

        var act = () => CurrentUser.FromClaimsPrincipal(principal);

        act.Should().Throw<UnauthorizedException>();
    }
}
