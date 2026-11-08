using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;
using Ruvents.UI.Planning;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Planning;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class ViewerTimeZoneTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolveAsyncReadsTheBrowserZoneOnceAndReportsItConfirmedAsync()
    {
        var jsRuntime = new FakeJsRuntime(Task.FromResult<string?>("Asia/Tokyo"));
        var viewerTimeZone = new ViewerTimeZone(jsRuntime);

        viewerTimeZone.ResolvedZoneId.ShouldBeNull();
        viewerTimeZone.IsConfirmed.ShouldBeFalse();

        (await viewerTimeZone.ResolveAsync(Token)).ShouldBe("Asia/Tokyo");
        viewerTimeZone.ResolvedZoneId.ShouldBe("Asia/Tokyo");
        viewerTimeZone.IsConfirmed.ShouldBeTrue();

        (await viewerTimeZone.ResolveAsync(Token)).ShouldBe("Asia/Tokyo");
        jsRuntime.Lookups.ShouldBe(1);
    }

    [Fact]
    public async Task CancelingOneCallerDoesNotPoisonTheSharedLookupAsync()
    {
        var lookup = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jsRuntime = new FakeJsRuntime(lookup.Task);
        var viewerTimeZone = new ViewerTimeZone(jsRuntime);
        using var cancellation = new CancellationTokenSource();

        var canceledCaller = viewerTimeZone.ResolveAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => canceledCaller);

        lookup.SetResult("Europe/Berlin");

        (await viewerTimeZone.ResolveAsync(Token)).ShouldBe("Europe/Berlin");
        jsRuntime.Lookups.ShouldBe(1);
    }

    [Fact]
    public async Task AnUnavailableJavaScriptRuntimeLeavesTheZoneUnconfirmedAsync()
    {
        var viewerTimeZone = new ViewerTimeZone(new ThrowingJsRuntime());

        (await viewerTimeZone.ResolveAsync(Token)).ShouldBeNull();
        viewerTimeZone.ResolvedZoneId.ShouldBeNull();
        viewerTimeZone.IsConfirmed.ShouldBeFalse();
    }

    private sealed class FakeJsRuntime(Task<string?> zoneTask) : IJSRuntime
    {
        private int _lookups;

        public int Lookups => Volatile.Read(ref _lookups);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "The caller owns the module reference and disposes it.")]
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (typeof(TValue) != typeof(IJSObjectReference))
            {
                throw new InvalidOperationException($"Unexpected JavaScript interop type {typeof(TValue)}.");
            }

            Interlocked.Increment(ref _lookups);
            IJSObjectReference module = new FakeJsModule(zoneTask);
            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    private sealed class FakeJsModule(Task<string?> zoneTask) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (typeof(TValue) != typeof(string))
            {
                throw new InvalidOperationException($"Unexpected JavaScript interop type {typeof(TValue)}.");
            }

            return new ValueTask<TValue>((Task<TValue>)(object)zoneTask);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException("interop unavailable");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new JSException("interop unavailable");
    }
}
