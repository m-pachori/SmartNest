using FluentAssertions;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.Shared.Tests.Security;

public class AuthorizationGuardTests
{
    private static CurrentUser MakeUser(string[] roles, string? homeId) => new()
    {
        UserId = "user-1",
        Roles = roles,
        HomeId = homeId,
    };

    [Fact]
    public void RequireRole_DoesNotThrow_WhenUserHasAllowedRole()
    {
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician");

        act.Should().NotThrow();
    }

    [Fact]
    public void RequireRole_Throws_WhenUserHasNoneOfAllowedRoles()
    {
        var user = MakeUser(new[] { "SmartNest.Guest" }, "home-1");

        var act = () => AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void RequireHomeIdMatch_DoesNotThrow_WhenHomeIdsMatch()
    {
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => AuthorizationGuard.RequireHomeIdMatch(user, "home-1");

        act.Should().NotThrow();
    }

    [Fact]
    public void RequireHomeIdMatch_Throws_WhenHomeIdsDiffer()
    {
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => AuthorizationGuard.RequireHomeIdMatch(user, "home-2");

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void RequireHomeIdMatch_Throws_WhenUserHasNoHomeIdClaim()
    {
        var user = MakeUser(new[] { "SmartNest.Owner" }, homeId: null);

        var act = () => AuthorizationGuard.RequireHomeIdMatch(user, "home-1");

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void RequireOwnership_DoesNotThrow_WhenCallerIsResourceOwner()
    {
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => AuthorizationGuard.RequireOwnership(user, "user-1");

        act.Should().NotThrow();
    }

    [Fact]
    public void RequireOwnership_Throws_WhenCallerIsNotResourceOwner()
    {
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => AuthorizationGuard.RequireOwnership(user, "some-other-user");

        act.Should().Throw<ForbiddenException>();
    }
}
