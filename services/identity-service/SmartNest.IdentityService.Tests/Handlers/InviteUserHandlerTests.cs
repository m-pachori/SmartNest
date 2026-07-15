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

public class InviteUserHandlerTests
{
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static InviteUserRequest MakeRequest() => new("user-2", "SmartNest.Technician");

    [Fact]
    public async Task HandleAsync_CreatesMembershipAndPublishesEvent_WhenCallerOwnsTheHome()
    {
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetByHomeAndUserAsync("home-1", "user-2", It.IsAny<CancellationToken>())).ReturnsAsync((HomeMembership?)null);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new InviteUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var result = await handler.HandleAsync(user, "home-1", MakeRequest());

        result.HomeId.Should().Be("home-1");
        result.UserId.Should().Be("user-2");
        result.Role.Should().Be("SmartNest.Technician");
        repository.Verify(r => r.CreateAsync(It.IsAny<HomeMembership>(), It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "user-events",
                It.Is<EventEnvelope<UserInvitedPayload>>(e => e.EventType == "UserInvited"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new InviteUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Technician");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<HomeMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnTheHome()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("someone-else");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new InviteUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<HomeMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeDoesNotExist()
    {
        var repository = new Mock<IIdentityRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new InviteUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenActiveMembershipAlreadyExists()
    {
        var existingMembership = HomeMembership.Invite("home-1", "user-2", "SmartNest.Guest", "user-1");
        var repository = new Mock<IIdentityRepository>();
        repository.Setup(r => r.GetByHomeAndUserAsync("home-1", "user-2", It.IsAny<CancellationToken>())).ReturnsAsync(existingMembership);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new InviteUserHandler(repository.Object, homeOwnership.Object, new IdentityEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<HomeMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
