using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

/// <summary>
/// Exercises the real manager so the protected-token override is covered. Substituting the
/// base <c>UserManager</c> elsewhere cannot observe the token it forwards to the store.
/// </summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class CancellableUserManagerTests
{
    [Fact]
    public async Task StoreCallsReceiveTheScopedTokenAsync()
    {
        using var store = CreateStore();
        using var source = new CancellationTokenSource();
        var cancellation = new IdentityCancellation { Token = source.Token };
        using var manager = CreateManager(store, cancellation);

        await manager.SetUserNameAsync(new ApplicationUser(), "member@example.test");

        await store.Received(1).SetUserNameAsync(Arg.Any<ApplicationUser>(), "member@example.test", source.Token);
    }

    [Fact]
    public async Task CancelledRequestDoesNotReachTheStoreWriteAsync()
    {
        using var store = CreateStore();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var cancellation = new IdentityCancellation { Token = source.Token };
        using var manager = CreateManager(store, cancellation);
        store.SetUserNameAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<CancellationToken>().IsCancellationRequested
                ? Task.FromException(new OperationCanceledException(call.Arg<CancellationToken>()))
                : Task.CompletedTask);

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.SetUserNameAsync(new ApplicationUser(), "member@example.test"));

        await store.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreCallsReceiveNoneAfterCompleteWriteEvenWhenTheRequestTokenIsCancelledAsync()
    {
        using var store = CreateStore();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var cancellation = new IdentityCancellation { Token = source.Token };
        using var manager = CreateManager(store, cancellation);

        cancellation.CompleteWrite();
        await manager.SetUserNameAsync(new ApplicationUser(), "member@example.test");

        await store.Received(1).SetUserNameAsync(Arg.Any<ApplicationUser>(), "member@example.test", CancellationToken.None);
    }

    [Fact]
    public async Task RecoveryCodeRedemptionClearsTheTokenAtTheWriteBoundaryAsync()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>, IUserTwoFactorRecoveryCodeStore<ApplicationUser>>();
        store.UpdateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);
        using var source = new CancellationTokenSource();
        var cancellation = new IdentityCancellation { Token = source.Token };
        using var manager = CreateManager(store, cancellation);
        var user = new ApplicationUser();
        // Abort as the store commits the redemption, before SignInManager completes sign-in.
        ((IUserTwoFactorRecoveryCodeStore<ApplicationUser>)store)
            .RedeemCodeAsync(user, "ABC-123", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await source.CancelAsync();
                return true;
            });

        var result = await manager.RedeemTwoFactorRecoveryCodeAsync(user, "ABC-123");

        result.Succeeded.ShouldBeTrue();
        cancellation.Token.ShouldBe(CancellationToken.None);
    }

    private static IUserStore<ApplicationUser> CreateStore()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        store.SetUserNameAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        store.UpdateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(IdentityResult.Success);
        return store;
    }

    private static CancellableUserManager CreateManager(IUserStore<ApplicationUser> store, IdentityCancellation cancellation)
        => new(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            NullLogger<CancellableUserManager>.Instance,
            cancellation);
}
