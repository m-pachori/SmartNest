using FluentAssertions;
using Moq;
using SmartNest.IdentityService.Domain;
using SmartNest.IdentityService.Dtos;
using SmartNest.IdentityService.Events;
using SmartNest.IdentityService.Handlers;
using SmartNest.IdentityService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.IdentityService.Tests.Handlers;

public class UpdateUserRoleHandlerTests
{
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static HomeMembership MakeMembership(string homeId = "home-1") =>
        HomeMembership.Invite(homeId, "user-2", "SmartNest.Guest", "user-1");

    private static UpdateUserRoleRequest MakeRequest() => new("SmartNest.Technician");

    [Fact]
    public async Task HandleAsync_UpdatesRoleAndPublishesEvent_WhenCallerOwnsTheHome()
    {
        var membership = MakeMembership();
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetAsync(membership.MembershipId, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new UpdateUserRoleHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var result = await handler.HandleAsync(user, membership.MembershipId, MakeRequest());

        result.Role.Should().Be("SmartNest.Technician");
        repository.Verify(r => r.UpdateAsync(membership, It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "user-events",
                It.Is<EventEnvelope<RoleAssignedPayload>>(e => e.EventType == "RoleAssigned"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new UpdateUserRoleHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Guest");

        var act = () => handler.HandleAsync(user, "membership-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenMembershipNotFound()
    {
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((HomeMembership?)null);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new UpdateUserRoleHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "missing", MakeRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnTheHome()
    {
        var membership = MakeMembership();
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetAsync(membership.MembershipId, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("someone-else");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new UpdateUserRoleHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, membership.MembershipId, MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.UpdateAsync(It.IsAny<HomeMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
