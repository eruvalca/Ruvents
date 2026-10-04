using System.Diagnostics.CodeAnalysis;

namespace Ruvents.UI.Components;

/// <summary>Own one per independently replaceable activity. Access on the component's renderer context.</summary>
internal sealed class LatestOperation : IDisposable
{
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The operation's caller owns disposal after the in-flight work finishes; this owner only signals cancellation.")]
    private Operation? _current;
    private bool _disposed;

    public Operation Begin(TimeSpan timeout, CancellationToken lifetime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfCancellationRequested();
        var next = new Operation(this, timeout, lifetime);
        var previous = _current;
        _current = next;
        previous?.Cancel();
        return next;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        var previous = _current;
        _current = null;
        previous?.Cancel();
        // The operation disposes its source after its work finishes, even if cancellation is ignored.
    }

    public sealed class Operation(LatestOperation owner, TimeSpan timeout, CancellationToken lifetime) : IDisposable
    {
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This is a reference to the containing activity, not a resource owned by the operation.")]
        private readonly LatestOperation _owner = owner;
        private readonly CancellationTokenSource _source = CreateSource(timeout, lifetime);
        private bool _disposed;

        private static CancellationTokenSource CreateSource(TimeSpan timeout, CancellationToken lifetime)
        {
            // Validate before allocating a source so invalid timeouts cannot leak a linked registration.
            ArgumentOutOfRangeException.ThrowIfLessThan(timeout, Timeout.InfiniteTimeSpan);
            if (timeout.TotalMilliseconds > uint.MaxValue - 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The timeout exceeds the supported timer duration.");
            }
            var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            source.CancelAfter(timeout);
            return source;
        }

        public CancellationToken Token => _source.Token;
        public bool IsCurrent => !_owner._disposed && ReferenceEquals(_owner._current, this);

        internal void Cancel() => _source.Cancel();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (IsCurrent)
            {
                _owner._current = null;
            }
            _source.Dispose();
        }
    }
}
