using Microsoft.AspNetCore.Components;

namespace Ruvents.UI.Components;

/// <summary>Owns cancellation for component work without depending on a server HTTP context.</summary>
public abstract class CancellableComponentBase : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _disposal;

    // The static App supplies this cascade. Cascades do not cross an interactive renderer boundary.
    [CascadingParameter(Name = nameof(RequestAborted))]
    public CancellationToken RequestAborted { get; set; }

    protected bool IsDisposed { get; private set; }

    protected CancellationToken LifetimeToken => IsDisposed ? new CancellationToken(canceled: true) : _lifetime.Token;

    // Each asynchronous operation owns its linked source until its work has finished.
    protected CancellationTokenSource CreateOperation()
        => CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken, RequestAborted);

    private CancellationTokenSource? _latestOperation;
    private int _latestVersion;

    /// <summary>
    /// Runs a read so a newer call supersedes it: the earlier operation is cancelled and its
    /// late result is discarded instead of overwriting newer state.
    /// </summary>
    protected async Task LoadLatestAsync<T>(Func<CancellationToken, Task<T>> load, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(apply);

        // The previous operation disposes its own source when its work unwinds; cancelling
        // is enough here and avoids pulling a source out from under a live await.
        if (_latestOperation is not null)
        {
            await _latestOperation.CancelAsync();
        }

        using var operation = CreateOperation();
        _latestOperation = operation;
        var version = ++_latestVersion;
        try
        {
            var result = await load(operation.Token);
            if (version == _latestVersion)
            {
                apply(result);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer load superseded this one, or the component is going away.
        }
        finally
        {
            if (ReferenceEquals(_latestOperation, operation))
            {
                _latestOperation = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposal ??= DisposeComponentAsync();
        await _disposal;
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeCoreAsync() => ValueTask.CompletedTask;

    private async Task DisposeComponentAsync()
    {
        IsDisposed = true;
        try
        {
            try
            {
                await _lifetime.CancelAsync();
            }
            finally
            {
                await DisposeCoreAsync();
            }
        }
        finally
        {
            _latestOperation?.Dispose();
            _latestOperation = null;
            _lifetime.Dispose();
        }
    }
}
