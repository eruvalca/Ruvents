using System.Diagnostics.CodeAnalysis;
using Bunit;
using Ruvents.UI.Components;
using Shouldly;
using Xunit;
using TestContext = Xunit.TestContext;

namespace Ruvents.ComponentTests.Components;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class CancellationTests
{
    [Fact]
    public async Task RendererDisposalCancelsWorkAndRunsCleanupOnceAsync()
    {
        await using var context = new BunitContext();
        var component = context.Render<LifetimeProbe>();
        var instance = component.Instance;
        var token = instance.Token;
        token.CanBeCanceled.ShouldBeTrue();
        token.IsCancellationRequested.ShouldBeFalse();

        await context.DisposeAsync();
        await instance.DisposeAsync();

        token.IsCancellationRequested.ShouldBeTrue();
        instance.Token.IsCancellationRequested.ShouldBeTrue();
        instance.CleanupCalls.ShouldBe(1);
        instance.CanceledDuringCleanup.ShouldBeTrue();
    }

    [Fact]
    public async Task SupersededOperationRemainsUsableUntilItsCallerFinishesAsync()
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var loads = new LatestOperation();
        using var first = loads.Begin(Timeout.InfiniteTimeSpan, lifetime.Token);
        var firstToken = first.Token;
        using var second = loads.Begin(Timeout.InfiniteTimeSpan, lifetime.Token);

        firstToken.IsCancellationRequested.ShouldBeTrue();
        first.IsCurrent.ShouldBeFalse();
        second.IsCurrent.ShouldBeTrue();
        second.Token.IsCancellationRequested.ShouldBeFalse();
        var observed = false;
        using var registration = first.Token.Register(() => observed = true);
        observed.ShouldBeTrue();

        await lifetime.CancelAsync();
        second.Token.IsCancellationRequested.ShouldBeTrue();
        loads.Dispose();
        second.IsCurrent.ShouldBeFalse();
        Should.Throw<ObjectDisposedException>(() => loads.Begin(Timeout.InfiniteTimeSpan, lifetime.Token));
    }

    [Fact]
    public async Task DeadlineCancelsOnlyItsOperationAsync()
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var firstActivity = new LatestOperation();
        using var secondActivity = new LatestOperation();
        using var timed = firstActivity.Begin(TimeSpan.Zero, lifetime.Token);
        using var independent = secondActivity.Begin(Timeout.InfiniteTimeSpan, lifetime.Token);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = timed.Token.Register(() => canceled.TrySetResult());

        await canceled.Task.WaitAsync(TestContext.Current.CancellationToken);

        timed.IsCurrent.ShouldBeTrue();
        lifetime.IsCancellationRequested.ShouldBeFalse();
        independent.Token.IsCancellationRequested.ShouldBeFalse();
    }

    private sealed class LifetimeProbe : CancelableComponentBase
    {
        public CancellationToken Token => ComponentCancellationToken;
        public int CleanupCalls { get; private set; }
        public bool CanceledDuringCleanup { get; private set; }

        protected override ValueTask OnDisposeAsync()
        {
            CleanupCalls++;
            CanceledDuringCleanup = Token.IsCancellationRequested;
            return ValueTask.CompletedTask;
        }
    }
}
