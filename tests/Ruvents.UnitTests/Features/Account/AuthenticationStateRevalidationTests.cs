using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The provider owns each substituted scope; tests verify asynchronous disposal.")]
public sealed class AuthenticationStateRevalidationTests
{
    [Theory]
    [InlineData("stamp", "stamp", true)]
    [InlineData("stamp", "changed", false)]
    [InlineData("stamp", "STAMP", false)]
    [InlineData(null, "stamp", false)]
    public async Task SecurityStampMustMatchConfiguredClaimExactlyAsync(string? claimStamp, string storedStamp, bool expected)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var store = Substitute.For<IUserStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>>();
        var stamps = (IUserSecurityStampStore<ApplicationUser>)store;
        var user = new ApplicationUser();
        store.FindByIdAsync("member", cancellation.Token).Returns(user);
        stamps.GetSecurityStampAsync(user, cancellation.Token).Returns(storedStamp);
        var scope = CreateScope(store);
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(scope);
        var options = new IdentityOptions();
        options.ClaimsIdentity.UserIdClaimType = "ruvents:id";
        options.ClaimsIdentity.SecurityStampClaimType = "ruvents:stamp";
        using var provider = new IdentityRevalidatingAuthenticationStateProvider(NullLoggerFactory.Instance, scopes, Options.Create(options));
        var claims = new List<Claim> { new("ruvents:id", "member") };
        if (claimStamp is not null)
        {
            claims.Add(new("ruvents:stamp", claimStamp));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        (await ValidateAsync(provider, principal, cancellation.Token)).ShouldBe(expected);

        await stamps.Received(1).GetSecurityStampAsync(user, cancellation.Token);
        await ((IAsyncDisposable)scope).Received(1).DisposeAsync();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task StoreWithoutSecurityStampsRequiresExistingUserAsync(bool exists, bool expected)
    {
        using var store = Substitute.For<IUserStore<ApplicationUser>>();
        store.FindByIdAsync("member", TestContext.Current.CancellationToken).Returns(exists ? new ApplicationUser() : null);
        var scope = CreateScope(store);
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(scope);
        using var provider = CreateProvider(scopes);

        (await ValidateAsync(provider, Principal(), TestContext.Current.CancellationToken)).ShouldBe(expected);

        await ((IAsyncDisposable)scope).Received(1).DisposeAsync();
    }

    [Fact]
    public async Task EachRevalidationUsesFreshScopeAndObservesDeletedUserAsync()
    {
        using var firstStore = Substitute.For<IUserStore<ApplicationUser>>();
        using var secondStore = Substitute.For<IUserStore<ApplicationUser>>();
        firstStore.FindByIdAsync("member", TestContext.Current.CancellationToken).Returns(new ApplicationUser());
        secondStore.FindByIdAsync("member", TestContext.Current.CancellationToken).Returns((ApplicationUser?)null);
        var firstScope = CreateScope(firstStore);
        var secondScope = CreateScope(secondStore);
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(firstScope, secondScope);
        using var provider = CreateProvider(scopes);

        (await ValidateAsync(provider, Principal(), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await ValidateAsync(provider, Principal(), TestContext.Current.CancellationToken)).ShouldBeFalse();

        scopes.Received(2).CreateScope();
        await ((IAsyncDisposable)firstScope).Received(1).DisposeAsync();
        await ((IAsyncDisposable)secondScope).Received(1).DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationCannotInvalidateReplacementStateEvenWhenStoreIgnoresItAsync(bool storeThrows)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var store = Substitute.For<IUserStore<ApplicationUser>>();
        store.FindByIdAsync("member", cancellation.Token).Returns(async _ =>
        {
            await cancellation.CancelAsync();
            if (storeThrows)
            {
                throw new OperationCanceledException(cancellation.Token);
            }
            return null; // A stale missing-user result must never sign out the new authentication state.
        });
        var scope = CreateScope(store);
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(scope);
        using var provider = CreateProvider(scopes);

        var failure = await Should.ThrowAsync<TaskCanceledException>(() => ValidateAsync(provider, Principal(), cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        await store.Received(1).FindByIdAsync("member", cancellation.Token);
        await ((IAsyncDisposable)scope).Received(1).DisposeAsync();
    }

    [Fact]
    public async Task UnrelatedCancellationRemainsAnUnexpectedFailureAsync()
    {
        using var store = Substitute.For<IUserStore<ApplicationUser>>();
        var failure = new OperationCanceledException("Dependency canceled independently");
        store.FindByIdAsync("member", TestContext.Current.CancellationToken).Returns(Task.FromException<ApplicationUser?>(failure));
        var scope = CreateScope(store);
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(scope);
        using var provider = CreateProvider(scopes);

        var thrown = await Should.ThrowAsync<OperationCanceledException>(async () => await ValidateAsync(provider, Principal(), TestContext.Current.CancellationToken));
        thrown.CancellationToken.ShouldNotBe(TestContext.Current.CancellationToken);
        await ((IAsyncDisposable)scope).Received(1).DisposeAsync();
    }

    [Fact]
    public async Task MissingIdDoesNotQueryStoreAsync()
    {
        using var store = Substitute.For<IUserStore<ApplicationUser>>();
        var scopes = Substitute.For<IServiceScopeFactory>();
        var scope = CreateScope(store);
        scopes.CreateScope().Returns(scope);
        using var provider = CreateProvider(scopes);

        (await ValidateAsync(provider, new ClaimsPrincipal(), TestContext.Current.CancellationToken)).ShouldBeFalse();

        await store.DidNotReceiveWithAnyArgs().FindByIdAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnexpectedStoreFailureStillDisposesScopeAsync()
    {
        using var store = Substitute.For<IUserStore<ApplicationUser>>();
        var failure = new InvalidOperationException("Store unavailable");
        store.FindByIdAsync("member", TestContext.Current.CancellationToken).Returns(Task.FromException<ApplicationUser?>(failure));
        var scope = CreateScope(store);
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(scope);
        using var provider = CreateProvider(scopes);

        (await Should.ThrowAsync<InvalidOperationException>(async () => await ValidateAsync(provider, Principal(), TestContext.Current.CancellationToken)))
            .ShouldBeSameAs(failure);
        await ((IAsyncDisposable)scope).Received(1).DisposeAsync();
    }

    [Fact]
    public async Task AlreadyCanceledRevalidationDoesNotCreateScopeAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var scopes = Substitute.For<IServiceScopeFactory>();
        using var provider = CreateProvider(scopes);

        var failure = await Should.ThrowAsync<TaskCanceledException>(async () => await ValidateAsync(provider, Principal(), cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        scopes.DidNotReceive().CreateScope();
    }

    private static IdentityRevalidatingAuthenticationStateProvider CreateProvider(IServiceScopeFactory scopes) =>
        new(NullLoggerFactory.Instance, scopes, Options.Create(new IdentityOptions()));

    private static ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "member")], "test"));

    private static IServiceScope CreateScope(IUserStore<ApplicationUser> store)
    {
        var scope = Substitute.For<IServiceScope, IAsyncDisposable>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IUserStore<ApplicationUser>)).Returns(store);
        scope.ServiceProvider.Returns(services);
        return scope;
    }

    private static Task<bool> ValidateAsync(IdentityRevalidatingAuthenticationStateProvider provider, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        // Invoke the framework's protected hook rather than wait for its 30-minute timer.
        var validate = typeof(IdentityRevalidatingAuthenticationStateProvider)
            .GetMethod("ValidateAuthenticationStateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<AuthenticationState, CancellationToken, Task<bool>>>(provider);
        return validate(new AuthenticationState(principal), cancellationToken);
    }
}
