using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;
using SmartNest.PlatformService.Dtos.Automation;
using SmartNest.PlatformService.Handlers.Automation;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Tests.Handlers.Automation;

public class CreateRuleHandlerTests
{
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static CreateRuleRequest MakeRequest() => new(
        DeviceId: "device-1",
        Name: "Hot Alert",
        Condition: new ConditionRequest("temperature", ConditionOperator.GreaterThan, "30"),
        Action: new RuleActionRequest(RuleActionType.RaiseAlert, null, null, null, "Warning", "Too hot"));

    [Fact]
    public async Task HandleAsync_CreatesRule_WhenCallerIsOwnerAndOwnsHome()
    {
        var repository = new Mock<IRuleRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(r => r.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var handler = new CreateRuleHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Owner");

        var result = await handler.HandleAsync(user, "home-1", MakeRequest());

        result.Name.Should().Be("Hot Alert");
        repository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Automation.Rule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IRuleRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var handler = new CreateRuleHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Guest");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Automation.Rule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnHome()
    {
        var repository = new Mock<IRuleRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(r => r.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("someone-else");
        var handler = new CreateRuleHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IRuleRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(r => r.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var handler = new CreateRuleHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
