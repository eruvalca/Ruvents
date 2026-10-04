using Microsoft.AspNetCore.Components;

namespace Ruvents.UI.Components;

/// <summary>Owns cancellation for one component instance. Adopt only for components that own asynchronous work.</summary>
public abstract class CancelableComponentBase : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _disposalTask;

    protected bool IsDisposed { get; private set; }

    protected CancellationToken ComponentCancellationToken => IsDisposed
        ? new CancellationToken(canceled: true)
        : _lifetime.Token;

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return new(_disposalTask ??= DisposeCoreAsync());
    }

    /// <summary>Release subscriptions and owned resources here; never update rendered state during disposal.</summary>
    protected virtual ValueTask OnDisposeAsync() => ValueTask.CompletedTask;

    private async Task DisposeCoreAsync()
    {
        IsDisposed = true;
        try
        {
            // This waits for callbacks, not for every operation using the token.
            await _lifetime.CancelAsync();
        }
        finally
        {
            try
            {
                await OnDisposeAsync();
            }
            finally
            {
                _lifetime.Dispose();
            }
        }
    }
}
