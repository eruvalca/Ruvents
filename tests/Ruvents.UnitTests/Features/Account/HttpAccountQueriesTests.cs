using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Ruvents.Client.Features.Account;
using Ruvents.SharedKernel.Features.Account;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class HttpAccountQueriesTests
{
    [Fact]
    public async Task CancellationReachesPendingHttpSendAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        using var handler = new Handler(async (_, token) =>
        {
            observed = token;
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var task = new HttpAccountQueries(http).GetCurrentAsync(cancellation.Token);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => task);
        observed.IsCancellationRequested.ShouldBeTrue();
        task.IsCanceled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ReadsSummaryOrMissingAccountAsync(HttpStatusCode status)
    {
        using var handler = new Handler((request, _) =>
        {
            request.RequestUri.ShouldBe(new Uri("https://example.test/base/api/account/summary"));
            return Task.FromResult(new HttpResponseMessage(status) { Content = JsonContent.Create(new AccountSummary("member", true)) });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/base/") };

        var summary = await new HttpAccountQueries(http).GetCurrentAsync(TestContext.Current.CancellationToken);

        if (status == HttpStatusCode.NotFound)
        {
            summary.ShouldBeNull();
        }
        else
        {
            summary.ShouldBe(new AccountSummary("member", true));
        }
    }

    [Fact]
    public async Task AuthorizationFailureIsNotReportedAsMissingAccountAsync()
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        var error = await Should.ThrowAsync<HttpRequestException>(() => new HttpAccountQueries(http).GetCurrentAsync(TestContext.Current.CancellationToken));

        error.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    [Fact]
    public async Task CancellationAlsoReachesResponseBodyAfterHeadersArriveAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var body = new PendingBody();
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var pending = new HttpAccountQueries(http).GetCurrentAsync(cancellation.Token);
        await body.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        body.ObservedToken.IsCancellationRequested.ShouldBeTrue();
    }

    private sealed class PendingBody : HttpContent
    {
        public CancellationToken ObservedToken { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            ObservedToken = cancellationToken;
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Stream.Null;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
