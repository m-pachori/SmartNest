using FluentAssertions;
using Moq;
using SmartNest.IdentityService.Domain;
using SmartNest.IdentityService.Events;
using SmartNest.IdentityService.Handlers;
using SmartNest.IdentityService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.IdentityService.Tests.Handlers;

public class RemoveUserHandlerTests
{
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static HomeMembership MakeMembership(string homeId = "home-1") =>
        HomeMembership.Invite(homeId, "user-2", "SmartNest.Guest", "user-1");

    [Fact]
    public async Task HandleAsync_DeactivatesMembershipAndPublishesEvent_WhenCallerOwnsTheHome()
    {
        var membership = MakeMembership();
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetByHomeAndUserAsync("home-1", "user-2", It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        await handler.HandleAsync(user, "home-1", "user-2");

        repository.Verify(r => r.UpdateAsync(membership, It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "user-events",
                It.Is<EventEnvelope<UserDeactivatedPayload>>(e => e.EventType == "UserDeactivated"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Guest");

        var act = () => handler.HandleAsync(user, "home-1", "user-2");

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.GetByHomeAndUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeDoesNotExist()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", "user-2");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnTheHome()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("someone-else");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", "user-2");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNoActiveMembershipExists()
    {
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetByHomeAndUserAsync("home-1", "user-2", It.IsAny<CancellationToken>())).ReturnsAsync((HomeMembership?)null);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", "user-2");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
