using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Models;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountEmailChangeServiceTests
{
    private static readonly string[] _successSteps = ["email", "username", "refresh"];

    [Fact]
    public async Task SuccessfulChangeUpdatesBothNamesBeforeRefreshingSignInAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser();
        var steps = new List<string>();
        identity.Users.ChangeEmailAsync(user, "new@example.test", "token").Returns(_ =>
        {
            steps.Add("email");
            return IdentityResult.Success;
        });
        identity.Users.SetUserNameAsync(user, "new@example.test").Returns(_ =>
        {
            steps.Add("username");
            return IdentityResult.Success;
        });
        identity.SignIn.RefreshSignInAsync(user).Returns(_ =>
        {
            steps.Add("refresh");
            return Task.CompletedTask;
        });
        var service = new AccountEmailChangeService(identity.Users, identity.SignIn, identity.Cancellation);

        var result = await service.ChangeAsync(user, "new@example.test", "token");

        result.Value.ShouldBeOfType<EmailChangeOutcome.Changed>();
        steps.ShouldBe(_successSteps);
    }

    [Fact]
    public async Task SuccessfulChangeClearsTheTokenSoFollowUpWorkCannotBeAbandonedAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser();
        using var cancelled = new CancellationTokenSource();
        identity.Cancellation.Token = cancelled.Token;
        // Abort as the email write completes, so the commit boundary is what protects the follow-up.
        identity.Users.ChangeEmailAsync(user, "new@example.test", "token").Returns(async _ =>
        {
            await cancelled.CancelAsync();
            return IdentityResult.Success;
        });
        identity.Users.SetUserNameAsync(user, "new@example.test").Returns(IdentityResult.Success);
        var service = new AccountEmailChangeService(identity.Users, identity.SignIn, identity.Cancellation);

        var result = await service.ChangeAsync(user, "new@example.test", "token");

        result.Value.ShouldBeOfType<EmailChangeOutcome.Changed>();
        await identity.Users.Received(1).SetUserNameAsync(user, "new@example.test");
        await identity.SignIn.Received(1).RefreshSignInAsync(user);
        identity.Cancellation.Token.ShouldBe(CancellationToken.None);
    }

    [Fact]
    public async Task AlreadyCancelledRequestPerformsNoWriteAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        identity.Cancellation.Token = cancelled.Token;
        var service = new AccountEmailChangeService(identity.Users, identity.SignIn, identity.Cancellation);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ChangeAsync(user, "new@example.test", "token"));

        await identity.Users.DidNotReceiveWithAnyArgs().ChangeEmailAsync(default!, default!, default!);
        await identity.Users.DidNotReceiveWithAnyArgs().SetUserNameAsync(default!, default!);
        await identity.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
        identity.Cancellation.Token.ShouldBe(cancelled.Token);
    }

    [Fact]
    public async Task EmailRejectionPreservesErrorAndSkipsUsernameAndRefreshAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser();
        identity.Users.ChangeEmailAsync(user, "new@example.test", "token").Returns(IdentityResult.Failed(
            new IdentityError { Code = "BadToken", Description = "Invalid confirmation token" }));
        var service = new AccountEmailChangeService(identity.Users, identity.SignIn, identity.Cancellation);

        var result = await service.ChangeAsync(user, "new@example.test", "token");

        var error = result.Value.ShouldBeOfType<EmailChangeOutcome.EmailChangeRejected>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("BadToken");
        error.Description.ShouldBe("Invalid confirmation token");
        await identity.Users.DidNotReceiveWithAnyArgs().SetUserNameAsync(default!, default!);
        await identity.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
    }

    [Fact]
    public async Task UsernameRejectionPreservesChangedEmailAndSkipsRefreshAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = "old@example.test", UserName = "old@example.test" };
        identity.Users.ChangeEmailAsync(user, "new@example.test", "token").Returns(_ =>
        {
            user.Email = "new@example.test";
            return IdentityResult.Success;
        });
        identity.Users.SetUserNameAsync(user, "new@example.test").Returns(IdentityResult.Failed(
            new IdentityError { Code = "NameTaken", Description = "Username is already in use" }));
        var service = new AccountEmailChangeService(identity.Users, identity.SignIn, identity.Cancellation);

        var result = await service.ChangeAsync(user, "new@example.test", "token");

        var error = result.Value.ShouldBeOfType<EmailChangeOutcome.UsernameChangeRejected>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("NameTaken");
        error.Description.ShouldBe("Username is already in use");
        user.Email.ShouldBe("new@example.test");
        user.UserName.ShouldBe("old@example.test");
        await identity.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
        await identity.Users.Received(1).ChangeEmailAsync(user, "new@example.test", "token");
    }
}
