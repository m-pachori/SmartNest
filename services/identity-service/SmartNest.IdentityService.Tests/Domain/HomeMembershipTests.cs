using FluentAssertions;
using SmartNest.IdentityService.Domain;
using SmartNest.IdentityService.Domain.Events;
using SmartNest.IdentityService.Domain.ValueObjects;
using Xunit;

namespace SmartNest.IdentityService.Tests.Domain;

public class HomeMembershipTests
{
    [Fact]
    public void Invite_SetsPropertiesAndRaisesUserInvitedEvent()
    {
        var membership = HomeMembership.Invite("home-1", "user-2", "SmartNest.Technician", "user-1");

        membership.MembershipId.Should().NotBeNullOrWhiteSpace();
        membership.HomeId.Should().Be("home-1");
        membership.UserId.Should().Be("user-2");
        membership.CurrentAssignment.Role.Should().Be("SmartNest.Technician");
        membership.CurrentAssignment.AssignedByUserId.Should().Be("user-1");
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new UserInvitedDomainEvent(membership.MembershipId, "home-1", "user-2", "SmartNest.Technician"));
    }

    [Theory]
    [InlineData("", "user-2")]
    [InlineData(" ", "user-2")]
    [InlineData("home-1", "")]
    [InlineData("home-1", " ")]
    public void Invite_Throws_WhenHomeIdOrUserIdMissing(string homeId, string userId)
    {
        var act = () => HomeMembership.Invite(homeId, userId, "SmartNest.Guest", "user-1");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignRole_UpdatesAssignmentAndRaisesRoleAssignedEvent()
    {
        var membership = HomeMembership.Invite("home-1", "user-2", "SmartNest.Guest", "user-1");
        membership.ClearDomainEvents();

        membership.AssignRole("SmartNest.Technician", "user-1");

        membership.CurrentAssignment.Role.Should().Be("SmartNest.Technician");
        membership.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new RoleAssignedDomainEvent(membership.MembershipId, "home-1", "user-2", "SmartNest.Technician"));
    }

    [Fact]
    public void Deactivate_SetsStatusAndRaisesUserDeactivatedEvent()
    {
        var membership = HomeMembership.Invite("home-1", "user-2", "SmartNest.Guest", "user-1");
        membership.ClearDomainEvents();

        membership.Deactivate();

        membership.Status.Should().Be(MembershipStatus.Deactivated);
        membership.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new UserDeactivatedDomainEvent(membership.MembershipId, "home-1", "user-2"));
    }
}
