using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountQueriesTests
{
    [Fact]
    public async Task CancellationStopsWaitingForAuthenticationWithoutOpeningDatabaseAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pendingState = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = Substitute.For<AuthenticationStateProvider>();
        state.GetAuthenticationStateAsync().Returns(pendingState.Task);
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        var queries = new AccountQueries(factory, state, Options.Create(new IdentityOptions()));

        var pending = queries.GetCurrentAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        pendingState.Task.IsCompleted.ShouldBeFalse();
        await factory.DidNotReceiveWithAnyArgs().CreateDbContextAsync(cancellation.Token);
        pendingState.SetResult(new AuthenticationState(new ClaimsPrincipal()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousOrMissingIdCannotOpenDatabaseAsync(bool authenticated)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], authenticated ? "test" : null));
        var state = Substitute.For<AuthenticationStateProvider>();
        state.GetAuthenticationStateAsync().Returns(new AuthenticationState(principal));
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        var queries = new AccountQueries(factory, state, Options.Create(new IdentityOptions()));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => queries.GetCurrentAsync(TestContext.Current.CancellationToken));

        await factory.DidNotReceiveWithAnyArgs().CreateDbContextAsync(TestContext.Current.CancellationToken);
    }
}
