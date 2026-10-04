using System.Diagnostics.CodeAnalysis;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Ruvents.SharedKernel.Features.Account;
using Ruvents.UI.Features.Account.Components;
using Shouldly;
using Xunit;
using TestContext = Xunit.TestContext;

namespace Ruvents.ComponentTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountSummaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SupersededResultsAndErrorsCannotOverwriteNewerLoadAsync(bool failOldLoad, bool newLoadFinishesFirst)
    {
        await using var context = new BunitContext();
        var queries = Configure(context);
        var oldLoad = new TaskCompletionSource<AccountSummary?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newLoad = new TaskCompletionSource<AccountSummary?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new List<CancellationToken>();
        queries.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            tokens.Add(call.Arg<CancellationToken>());
            return tokens.Count == 1 ? oldLoad.Task : newLoad.Task;
        });
        var component = context.Render<AccountSummaryPanel>();
        component.Find("[role='status']").TextContent.ShouldContain("Loading");

        var refresh = component.Find("fluent-button").ClickAsync();
        await component.WaitForAssertionAsync(() => tokens.Count.ShouldBe(2));
        tokens[0].IsCancellationRequested.ShouldBeTrue();
        tokens[1].IsCancellationRequested.ShouldBeFalse();
        if (newLoadFinishesFirst)
        {
            newLoad.SetResult(new AccountSummary("current", true));
            await refresh;
        }
        var previousRenderCount = component.RenderCount;
        if (failOldLoad)
        {
            oldLoad.SetException(new HttpRequestException("Old request failed"));
        }
        else
        {
            oldLoad.SetResult(new AccountSummary("old", false));
        }
        await component.WaitForAssertionAsync(() => component.RenderCount.ShouldBeGreaterThan(previousRenderCount));
        if (!newLoadFinishesFirst)
        {
            component.Find("[role='status']").TextContent.ShouldContain("Loading");
            newLoad.SetResult(new AccountSummary("current", true));
            await refresh;
        }

        await component.WaitForAssertionAsync(() => component.Markup.ShouldContain("Hello current!"));
        component.Markup.ShouldNotContain("Hello old!");
        component.FindAll("[role='alert']").ShouldBeEmpty();
        component.FindAll("[role='status']").ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposalCancelsTheActualServiceCallAsync()
    {
        await using var context = new BunitContext();
        var queries = Configure(context);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        queries.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            observed = call.Arg<CancellationToken>();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, observed);
                return null;
            }
            finally
            {
                finished.TrySetResult();
            }
        });
        _ = context.Render<AccountSummaryPanel>();

        await context.DisposeAsync();
        await finished.Task.WaitAsync(TestContext.Current.CancellationToken);

        observed.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task HttpFailureClearsLoadingAndRefreshRecoversAsync()
    {
        await using var context = new BunitContext();
        var queries = Configure(context);
        queries.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromException<AccountSummary?>(new HttpRequestException("Unavailable")),
            Task.FromResult<AccountSummary?>(new AccountSummary("recovered", true)));
        var component = context.Render<AccountSummaryPanel>();

        component.Find("[role='alert']").TextContent.ShouldContain("Please try again");
        component.FindAll("[role='status']").ShouldBeEmpty();
        await component.Find("fluent-button").ClickAsync();

        await component.WaitForAssertionAsync(() => component.Markup.ShouldContain("Hello recovered!"));
        component.FindAll("[role='alert']").ShouldBeEmpty();
    }

    [Fact]
    public async Task IndependentCancellationIsVisibleAndClearsLoadingAsync()
    {
        await using var context = new BunitContext();
        var queries = Configure(context);
        queries.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<AccountSummary?>(new OperationCanceledException()));

        var component = context.Render<AccountSummaryPanel>();

        component.Find("[role='alert']").TextContent.ShouldContain("interrupted");
        component.FindAll("[role='status']").ShouldBeEmpty();
    }

    [Fact]
    public async Task DeadlineShowsTimeoutAndClearsLoadingAsync()
    {
        await using var context = new BunitContext();
        var queries = Configure(context);
        queries.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return null;
        });

        var component = context.Render<AccountSummaryPanel>();

        await component.WaitForAssertionAsync(() => component.Find("[role='alert']").TextContent.ShouldContain("timed out"), TimeSpan.FromSeconds(20));
        component.FindAll("[role='status']").ShouldBeEmpty();
    }

    private static IAccountQueries Configure(BunitContext context)
    {
        context.AddAuthorization().SetAuthorized("member");
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        var queries = Substitute.For<IAccountQueries>();
        context.Services.AddSingleton(queries);
        context.Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return queries;
    }
}
