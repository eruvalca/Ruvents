using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Ruvents.SharedKernel.Security;
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
            CreateCancellation(identity.Cancellation.Token),
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
            CreateCancellation(TestContext.Current.CancellationToken),
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeFalse();
        await identity.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await promotion.DidNotReceiveWithAnyArgs().PromoteIfConfiguredAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnconfirmedAccountIsConfirmedAndPromotedWithThePostCommitTokenAsync()
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
            CreateCancellation(TestContext.Current.CancellationToken),
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeTrue();
        user.EmailConfirmed.ShouldBeTrue();
        await identity.Users.Received(1).UpdateAsync(user);
        await promotion.Received(1).PromoteIfConfiguredAsync(user, CancellationToken.None);
    }

    [Fact]
    public async Task CancellationAfterConfirmationStillPromotesAsync()
    {
        using var identity = IdentityTestContext.Create();
        using var cancellation = new CancellationTokenSource();
        var user = new ApplicationUser { Email = AdminEmail };
        identity.Users.FindByEmailAsync(AdminEmail).Returns(user);
        identity.Users.IsEmailConfirmedAsync(user).Returns(false);
        identity.Users.UpdateAsync(user).Returns(async _ =>
        {
            await cancellation.CancelAsync();
            return IdentityResult.Success;
        });
        var promotion = Substitute.For<IAdminPromotionService>();
        promotion.PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            CreateCancellation(cancellation.Token),
            AdminEmail,
            NullLogger.Instance,
            cancellation.Token);

        confirmed.ShouldBeTrue();
        await promotion.Received(1).PromoteIfConfiguredAsync(user, CancellationToken.None);
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
            CreateCancellation(TestContext.Current.CancellationToken),
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeTrue();
        await identity.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await promotion.Received(1).PromoteIfConfiguredAsync(user, CancellationToken.None);
    }

    [Fact]
    public async Task ExistingAdministratorCountsAsSuccessAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail, EmailConfirmed = true };
        identity.Users.FindByEmailAsync(AdminEmail).Returns(user);
        identity.Users.IsEmailConfirmedAsync(user).Returns(true);
        identity.Users.IsInRoleAsync(user, PlatformRoles.Admin).Returns(true);
        var promotion = Substitute.For<IAdminPromotionService>();
        promotion.PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            CreateCancellation(TestContext.Current.CancellationToken),
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeTrue();
        await identity.Users.Received(1).IsInRoleAsync(user, PlatformRoles.Admin);
    }

    [Fact]
    public async Task FailedPromotionFailsTheBootstrapAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail, EmailConfirmed = true };
        identity.Users.FindByEmailAsync(AdminEmail).Returns(user);
        identity.Users.IsEmailConfirmedAsync(user).Returns(true);
        identity.Users.IsInRoleAsync(user, PlatformRoles.Admin).Returns(false);
        var promotion = Substitute.For<IAdminPromotionService>();
        promotion.PromoteIfConfiguredAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        var confirmed = await PlatformAdminBootstrap.ConfirmAsync(
            identity.Users,
            promotion,
            CreateCancellation(TestContext.Current.CancellationToken),
            AdminEmail,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        confirmed.ShouldBeFalse();
        await identity.Users.Received(1).IsInRoleAsync(user, PlatformRoles.Admin);
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
            CreateCancellation(TestContext.Current.CancellationToken),
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
            CreateCancellation(cancellation.Token),
            AdminEmail,
            NullLogger.Instance,
            cancellation.Token));

        await identity.Users.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
    }

    private static IdentityCancellation CreateCancellation(CancellationToken token = default) => new() { Token = token };
}
