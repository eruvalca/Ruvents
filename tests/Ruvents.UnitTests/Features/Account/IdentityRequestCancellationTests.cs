using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class IdentityRequestCancellationTests
{
    [Fact]
    public async Task RequestAwareManagerForwardsAbortToStoreAsync()
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var store = Substitute.For<IUserStore<ApplicationUser>>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.FindByIdAsync("member", request.Token).Returns(async call =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return null;
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestAborted = request.Token } });
        services.AddSingleton(store);
        services.AddIdentityCore<ApplicationUser>().AddUserManager<AspNetUserManager<ApplicationUser>>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var pending = manager.FindByIdAsync("member");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await request.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        pending.IsCanceled.ShouldBeTrue();
        await store.Received(1).FindByIdAsync("member", request.Token);
    }

    [Fact]
    public async Task AlreadyCanceledRegistrationNeverCreatesAnAccountAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        using var identity = IdentityTestContext.Create();
        var service = new AccountRegistrationService(identity.Users, identity.Store);

        await Should.ThrowAsync<OperationCanceledException>(() => service.PasswordAsync("member@example.test", "secret", cancellation.Token));

        await identity.Store.DidNotReceiveWithAnyArgs().SetUserNameAsync(default!, default, cancellation.Token);
        await identity.Users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }
}
