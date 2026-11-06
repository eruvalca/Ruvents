using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class PlatformAdminBootstrapTests
{
    private const string AdminEmail = "admin@example.test";

    [Fact]
    public async Task MissingConfigurationDoesNotLookUpTheAccountAsync()
    {
        using var identity = IdentityTestContext.Create();

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            Substitute.For<IAdminPromotionService>(),
            adminEmail: null,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeFalse();
        await identity.Users.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
        await identity.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task UnknownAccountFailsWithoutConfirmingOrPromotingAsync()
    {
        using var identity = IdentityTestContext.Create();
        identity.Users.FindByEmailAsync(AdminEmail).Returns((ApplicationUser?)null);
        var promotion = Substitute.For<IAdminPromotionService>();

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeFalse();
        await identity.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await promotion.DidNotReceiveWithAnyArgs().PromoteIfConfiguredAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnconfirmedAccountIsConfirmedAndPromotedAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail };
        identity.Users.FindByEmailAsync(AdminEmail).Returns(user);
        identity.Users.IsEmailConfirmedAsync(user).Returns(false);
        identity.Users.UpdateAsync(user).Returns(IdentityResult.Success);
        var promotion = Substitute.For<IAdminPromotionService>();
        promotion.PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeTrue();
        user.EmailConfirmed.ShouldBeTrue();
        await identity.Users.Received(1).UpdateAsync(user);
        await promotion.Received(1).PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyConfirmedAccountSkipsTheUpdateAndIsPromotedAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail, EmailConfirmed = true };
        identity.Users.FindByEmailAsync(AdminEmail).Returns(user);
        identity.Users.IsEmailConfirmedAsync(user).Returns(true);
        var promotion = Substitute.For<IAdminPromotionService>();
        promotion.PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeTrue();
        await identity.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await promotion.Received(1).PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFailureSkipsPromotionAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail };
        identity.Users.FindByEmailAsync(AdminEmail).Returns(user);
        identity.Users.IsEmailConfirmedAsync(user).Returns(false);
        identity.Users.UpdateAsync(user).Returns(IdentityResult.Failed(new IdentityError { Description = "Concurrent update" }));
        var promotion = Substitute.For<IAdminPromotionService>();

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeFalse();
        user.EmailConfirmed.ShouldBeTrue();
        await promotion.DidNotReceiveWithAnyArgs().PromoteIfConfiguredAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CanceledRequestStopsBeforeLookingUpTheAccountAsync()
    {
        using var identity = IdentityTestContext.Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            Substitute.For<IAdminPromotionService>(),
            AdminEmail,
            NullLogger.Instance,
            cancellation.Token));

        await identity.Users.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
    }
}
