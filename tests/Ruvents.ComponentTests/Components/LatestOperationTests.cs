using System.Diagnostics.CodeAnalysis;
using Bunit;
using Microsoft.AspNetCore.Components;
using Ruvents.UI.Components;
using Shouldly;
using Xunit;

namespace Ruvents.ComponentTests.Components;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class LatestOperationTests
{
    [Fact]
    public async Task OlderLoadThatFinishesLastDoesNotOverwriteNewerResultAsync()
    {
        await using var context = new BunitContext();
        var loader = context.Render<Loader>();
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applied = new List<int>();

        var firstLoad = loader.Instance.LoadAsync(_ => first.Task, applied.Add);
        var secondLoad = loader.Instance.LoadAsync(_ => second.Task, applied.Add);

        second.SetResult(2);
        await secondLoad;
        first.SetResult(1);
        await firstLoad;

        applied.ShouldBe([2]);
    }

    [Fact]
    public async Task SupersedingLoadCancelsTheOlderOperationAsync()
    {
        await using var context = new BunitContext();
        var loader = context.Render<Loader>();
        CancellationToken observed = default;

        var firstLoad = loader.Instance.LoadAsync(
            async token =>
            {
                observed = token;
                // Waits until the load is superseded, which cancels this token.
                await Task.Delay(Timeout.Infinite, token);
                return 1;
            },
            _ => { });
        var secondLoad = loader.Instance.LoadAsync(_ => Task.FromResult(2), _ => { });
        await secondLoad;
        await firstLoad;

        observed.IsCancellationRequested.ShouldBeTrue();
    }

    // The base class member is protected, so a minimal component exposes it to the test.
    private sealed class Loader : CancellableComponentBase
    {
        internal Task LoadAsync(Func<CancellationToken, Task<int>> load, Action<int> apply) =>
            LoadLatestAsync(load, apply);
    }
}
