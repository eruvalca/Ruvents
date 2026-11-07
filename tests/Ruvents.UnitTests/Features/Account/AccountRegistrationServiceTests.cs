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
public sealed class AccountRegistrationServiceTests
{
    private static readonly string[] _passwordCreationSteps = ["username", "email", "create-password"];
    private static readonly string[] _creationErrorCodes = ["EmailTaken", "PasswordWeak"];
    private static readonly string[] _creationErrorDescriptions = ["Email already used", "Password is too weak"];

    [Fact]
    public async Task PasswordCreationInitializesUserBeforeCreatingAndReturnsSameUserAsync()
    {
        using var identity = IdentityTestContext.Create();
        var steps = ConfigureUserInitialization(identity);
        ApplicationUser? createdUser = null;
        identity.Users.CreateAsync(Arg.Any<ApplicationUser>(), "secret").Returns(call =>
        {
            steps.Add("create-password");
            createdUser = call.Arg<ApplicationUser>();
            return IdentityResult.Success;
        });
        var service = new AccountRegistrationService(identity.Users, identity.Store, identity.Cancellation, Substitute.For<IAdminPromotionService>());

        var result = await service.PasswordAsync("member@example.test", "secret", Name());

        var user = result.Value.ShouldBeOfType<RegistrationOutcome.Created>().User;
        user.ShouldBeSameAs(createdUser);
        user.UserName.ShouldBe("member@example.test");
        user.Email.ShouldBe("member@example.test");
        user.FirstName.ShouldBe("Ada");
        user.MiddleName.ShouldBe("Grace");
        user.LastName.ShouldBe("Lovelace");
        user.DisplayName.ShouldBe("Ada Grace Lovelace");
        steps.ShouldBe(_passwordCreationSteps);
        await identity.Users.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await identity.Users.DidNotReceiveWithAnyArgs().AddLoginAsync(default!, default!);
    }

    [Fact]
    public async Task PasswordCreationRejectionPreservesAllErrorsAsync()
    {
        using var identity = IdentityTestContext.Create();
        _ = ConfigureUserInitialization(identity);
        identity.Users.CreateAsync(Arg.Any<ApplicationUser>(), "secret").Returns(RejectedCreation());
        var service = new AccountRegistrationService(identity.Users, identity.Store, identity.Cancellation, Substitute.For<IAdminPromotionService>());

        var result = await service.PasswordAsync("member@example.test", "secret", Name());

        var errors = result.Value.ShouldBeOfType<RegistrationOutcome.CreationRejected>().Errors;
        errors.Select(error => error.Code).ShouldBe(_creationErrorCodes);
        errors.Select(error => error.Description).ShouldBe(_creationErrorDescriptions);
    }

    [Theory]
    [InlineData("", "Ada Lovelace")]
    [InlineData("Ada", "Ada")]
    public async Task DisplayNameUsesTheFullNameUnlessAnOverrideIsSuppliedAsync(string displayName, string expected)
    {
        using var identity = IdentityTestContext.Create();
        _ = ConfigureUserInitialization(identity);
        ApplicationUser? createdUser = null;
        identity.Users.CreateAsync(Arg.Any<ApplicationUser>(), "secret").Returns(call =>
        {
            createdUser = call.Arg<ApplicationUser>();
            return IdentityResult.Success;
        });
        var service = new AccountRegistrationService(identity.Users, identity.Store, identity.Cancellation, Substitute.For<IAdminPromotionService>());

        _ = await service.PasswordAsync(
            "member@example.test",
            "secret",
            new ProfileNameInput("Ada", string.Empty, "Lovelace", displayName));

        createdUser.ShouldNotBeNull();
        createdUser.DisplayName.ShouldBe(expected);
    }

    [Fact]
    public async Task UnsupportedEmailStoreIsAnUnexpectedConfigurationErrorAsync()
    {
        using var identity = IdentityTestContext.Create();
        identity.Users.SupportsUserEmail.Returns(false);
        var service = new AccountRegistrationService(identity.Users, identity.Store, identity.Cancellation, Substitute.For<IAdminPromotionService>());

        await Should.ThrowAsync<NotSupportedException>(() => service.PasswordAsync("member@example.test", "secret", Name()));

        await identity.Users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }

    [Fact]
    public async Task PasswordCreationStopsBeforeWritingWhenTheRequestIsCancelledAsync()
    {
        using var identity = IdentityTestContext.Create();
        _ = ConfigureUserInitialization(identity);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        identity.Cancellation.Token = cancelled.Token;
        var service = new AccountRegistrationService(identity.Users, identity.Store, identity.Cancellation, Substitute.For<IAdminPromotionService>());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.PasswordAsync("member@example.test", "secret", Name()));

        await identity.Users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }

    private static List<string> ConfigureUserInitialization(IdentityTestContext identity)
    {
        var steps = new List<string>();
        identity.Users.SupportsUserEmail.Returns(true);
        identity.Store.SetUserNameAsync(Arg.Any<ApplicationUser>(), "member@example.test", CancellationToken.None).Returns(call =>
        {
            steps.Add("username");
            call.Arg<ApplicationUser>().UserName = call.Arg<string>();
            return Task.CompletedTask;
        });
        identity.Store.SetEmailAsync(Arg.Any<ApplicationUser>(), "member@example.test", CancellationToken.None).Returns(call =>
        {
            steps.Add("email");
            call.Arg<ApplicationUser>().Email = call.Arg<string>();
            return Task.CompletedTask;
        });
        return steps;
    }

    private static ProfileNameInput Name(string? displayName = null) =>
        new("Ada", "Grace", "Lovelace", displayName);

    private static IdentityResult RejectedCreation() => IdentityResult.Failed(
        new IdentityError { Code = "EmailTaken", Description = "Email already used" },
        new IdentityError { Code = "PasswordWeak", Description = "Password is too weak" });
}
